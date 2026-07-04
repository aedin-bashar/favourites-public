using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Links;

public sealed class CleanupSuggestionsEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task CleanupSuggestions_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/links/cleanup-suggestions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CleanupSuggestions_NoArchivedLinks_ReturnsEmptyList()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "cleanup-none@example.com");

        var response = await client.GetAsync("/api/links/cleanup-suggestions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<LinkResponse>>();
        Assert.NotNull(body);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task CleanupSuggestions_RecentlyArchivedLink_IsNotReturned()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "cleanup-recent@example.com");

        // Create and archive a link right now — well within 90 days.
        var link = await CreateAndArchiveLinkAsync(client,
            "https://example.com/recent", "Recent Archived");

        var response = await client.GetAsync("/api/links/cleanup-suggestions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<LinkResponse>>();
        Assert.NotNull(body);
        Assert.DoesNotContain(body!, l => l.Id == link.Id);
    }

    [Fact]
    public async Task CleanupSuggestions_OldArchivedLink_IsReturned()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "cleanup-old@example.com");

        // Create and archive a link, then backdated to > 90 days ago in the DB.
        var link = await CreateAndArchiveLinkAsync(client,
            "https://example.com/old-archived", "Old Archived");

        await BackdateLinkAsync(factory, link.Id, DateTimeOffset.UtcNow.AddDays(-100));

        var response = await client.GetAsync("/api/links/cleanup-suggestions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<LinkResponse>>();
        Assert.NotNull(body);
        Assert.Contains(body!, l => l.Id == link.Id);
    }

    [Fact]
    public async Task CleanupSuggestions_ActiveLink_IsNeverReturned()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "cleanup-active@example.com");

        // Create a link but don't archive it; backdate it heavily.
        var linkResp = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com/active-not-archived", "Active", null));
        Assert.Equal(HttpStatusCode.Created, linkResp.StatusCode);
        var link = await linkResp.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(link);

        await BackdateLinkAsync(factory, link!.Id, DateTimeOffset.UtcNow.AddDays(-200));

        var response = await client.GetAsync("/api/links/cleanup-suggestions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<LinkResponse>>();
        Assert.NotNull(body);
        Assert.DoesNotContain(body!, l => l.Id == link.Id);
    }

    [Fact]
    public async Task CleanupSuggestions_OldestFirst_OrderIsRespected()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "cleanup-order@example.com");

        var link1 = await CreateAndArchiveLinkAsync(client,
            "https://example.com/order-1", "Order 1");
        var link2 = await CreateAndArchiveLinkAsync(client,
            "https://example.com/order-2", "Order 2");

        // link2 is older.
        await BackdateLinkAsync(factory, link1.Id, DateTimeOffset.UtcNow.AddDays(-95));
        await BackdateLinkAsync(factory, link2.Id, DateTimeOffset.UtcNow.AddDays(-120));

        var response = await client.GetAsync("/api/links/cleanup-suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<LinkResponse>>();
        Assert.NotNull(body);
        Assert.True(body!.Count >= 2);

        var idx1 = body.FindIndex(l => l.Id == link1.Id);
        var idx2 = body.FindIndex(l => l.Id == link2.Id);

        // link2 (older) should appear before link1.
        Assert.True(idx2 < idx1, "Older archived link should appear first.");
    }

    [Fact]
    public async Task CleanupSuggestions_OnlyReturnsCurrentUsersLinks()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "cleanup-iso-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "cleanup-iso-b@example.com");

        var bLink = await CreateAndArchiveLinkAsync(clientB,
            "https://example.com/b-old-archived", "B Old");
        await BackdateLinkAsync(factory, bLink.Id, DateTimeOffset.UtcNow.AddDays(-100));

        // User A should not see User B's link.
        var response = await clientA.GetAsync("/api/links/cleanup-suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<LinkResponse>>();
        Assert.NotNull(body);
        Assert.DoesNotContain(body!, l => l.Id == bLink.Id);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<LinkResponse> CreateAndArchiveLinkAsync(
        HttpClient client, string url, string title)
    {
        var createResp = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, null));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var link = await createResp.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(link);

        var archiveResp = await client.PostAsync($"/api/links/{link!.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveResp.StatusCode);

        return link;
    }

    private static async Task BackdateLinkAsync(
        FavouritesApiFactory factory, Guid linkId, DateTimeOffset timestamp)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var link = await db.FavouriteLinks.SingleAsync(l => l.Id == linkId);
        // Force the UpdatedAtUtc to the past so the cleanup threshold applies.
        db.Entry(link).Property("UpdatedAtUtc").CurrentValue = timestamp;
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> RegisterAndLoginAsync(HttpClient client, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email, ValidPassword, ValidPassword));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(registerBody);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, ValidPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return registerBody!.Id;
    }
}
