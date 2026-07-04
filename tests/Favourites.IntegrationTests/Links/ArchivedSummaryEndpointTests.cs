using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;

namespace Favourites.IntegrationTests.Links;

public sealed class ArchivedSummaryEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    // ── R8.10 — GET /api/archived/summary ──────────────────────────────────

    [Fact]
    public async Task GetArchivedSummary_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/archived/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetArchivedSummary_WithNoData_ReturnsAllZeros()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "archived-summary-empty@example.com");

        var response = await client.GetAsync("/api/archived/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ArchivedSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.ArchivedLinks);
        Assert.Equal(0, body.ArchivedThisMonth);
        Assert.Null(body.OldestArchived);
        Assert.Equal(0, body.RestoredRecently);
        Assert.Empty(body.CleanupSuggestions);
    }

    [Fact]
    public async Task GetArchivedSummary_CountsAreCorrect()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "archived-summary-counts@example.com");

        // Create 3 links and archive 2 of them
        var link1 = await CreateLinkAsync(client, "https://example.com/one", "One");
        var link2 = await CreateLinkAsync(client, "https://example.com/two", "Two");
        var link3 = await CreateLinkAsync(client, "https://example.com/three", "Three");

        await client.PostAsync($"/api/links/{link1.Id}/archive", null);
        await client.PostAsync($"/api/links/{link2.Id}/archive", null);

        var response = await client.GetAsync("/api/archived/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ArchivedSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.ArchivedLinks);
        // Archived this month: both were just archived
        Assert.Equal(2, body.ArchivedThisMonth);
        // Oldest archived must be one of the two archived links
        Assert.NotNull(body.OldestArchived);
        Assert.True(body.OldestArchived!.Id == link1.Id || body.OldestArchived.Id == link2.Id);
    }

    [Fact]
    public async Task GetArchivedSummary_IsUserScoped_DoesNotReturnOtherUsersData()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "archived-scope-a@example.com");
        var linkA = await CreateLinkAsync(clientA, "https://example.com/a", "A");
        await clientA.PostAsync($"/api/links/{linkA.Id}/archive", null);

        // ClientB has no archived links
        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "archived-scope-b@example.com");

        var response = await clientB.GetAsync("/api/archived/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ArchivedSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.ArchivedLinks);
        Assert.Null(body.OldestArchived);
    }

    private static async Task RegisterAndLoginAsync(HttpClient client, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email, ValidPassword, ValidPassword));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, ValidPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    private static async Task<LinkResponse> CreateLinkAsync(HttpClient client, string url, string title)
    {
        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(body);
        return body!;
    }
}
