using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;

namespace Favourites.IntegrationTests.Links;

public sealed class BulkArchivedEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    // ── R8.11 — POST /api/links/restore-many ───────────────────────────────

    [Fact]
    public async Task RestoreMany_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/links/restore-many",
            new RestoreManyLinksRequest([Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RestoreMany_EmptyList_ReturnsBadRequest()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "restore-many-empty@example.com");

        var response = await client.PostAsJsonAsync("/api/links/restore-many",
            new RestoreManyLinksRequest([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RestoreMany_RestoresOwnedArchivedLinks()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "restore-many-owned@example.com");

        var link1 = await CreateLinkAsync(client, "https://example.com/rm1", "RM1");
        var link2 = await CreateLinkAsync(client, "https://example.com/rm2", "RM2");
        await client.PostAsync($"/api/links/{link1.Id}/archive", null);
        await client.PostAsync($"/api/links/{link2.Id}/archive", null);

        var response = await client.PostAsJsonAsync("/api/links/restore-many",
            new RestoreManyLinksRequest([link1.Id, link2.Id]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Both links should now be active
        var listResponse = await client.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var pagedResult = await listResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(pagedResult);
        Assert.Contains(pagedResult!.Items, l => l.Id == link1.Id && !l.IsArchived);
        Assert.Contains(pagedResult.Items, l => l.Id == link2.Id && !l.IsArchived);
    }

    [Fact]
    public async Task RestoreMany_IgnoresLinksOwnedByOtherUsers()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "restore-many-owner@example.com");
        var linkA = await CreateLinkAsync(clientA, "https://example.com/owner-link", "Owner Link");
        await clientA.PostAsync($"/api/links/{linkA.Id}/archive", null);

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "restore-many-other@example.com");

        // ClientB tries to restore clientA's archived link
        var response = await clientB.PostAsJsonAsync("/api/links/restore-many",
            new RestoreManyLinksRequest([linkA.Id]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Link A must still be archived
        var archivedResponse = await clientA.GetAsync("/api/links?archived=archived");
        var pagedResult = await archivedResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(pagedResult);
        Assert.Contains(pagedResult!.Items, l => l.Id == linkA.Id && l.IsArchived);
    }

    // ── R8.11 — DELETE /api/links/archived ────────────────────────────────

    [Fact]
    public async Task DeleteArchived_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/links/archived");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteArchived_WithNoArchivedLinks_ReturnsZeroDeleted()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "delete-archived-empty@example.com");

        var response = await client.DeleteAsync("/api/links/archived");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteArchived_PermanentlyDeletesAllArchivedLinksForUser()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "delete-archived-owned@example.com");

        var link1 = await CreateLinkAsync(client, "https://example.com/da1", "DA1");
        var link2 = await CreateLinkAsync(client, "https://example.com/da2", "DA2");
        var activeLink = await CreateLinkAsync(client, "https://example.com/da3", "DA3-active");

        await client.PostAsync($"/api/links/{link1.Id}/archive", null);
        await client.PostAsync($"/api/links/{link2.Id}/archive", null);

        var deleteResponse = await client.DeleteAsync("/api/links/archived");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Archived list should now be empty
        var archivedResponse = await client.GetAsync("/api/links?archived=archived");
        var pagedResult = await archivedResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(pagedResult);
        Assert.Empty(pagedResult!.Items);

        // Active link must still be there
        var activeResponse = await client.GetAsync("/api/links");
        var activePaged = await activeResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(activePaged);
        Assert.Contains(activePaged!.Items, l => l.Id == activeLink.Id);
    }

    [Fact]
    public async Task DeleteArchived_OnlyDeletesCurrentUsersLinks()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "delete-archived-scope-a@example.com");
        var linkA = await CreateLinkAsync(clientA, "https://example.com/scope-a", "Scope A");
        await clientA.PostAsync($"/api/links/{linkA.Id}/archive", null);

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "delete-archived-scope-b@example.com");

        // ClientB empties their own archive (which is empty)
        var deleteResponse = await clientB.DeleteAsync("/api/links/archived");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // ClientA's archived link must still exist
        var archivedResponse = await clientA.GetAsync("/api/links?archived=archived");
        var pagedResult = await archivedResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(pagedResult);
        Assert.Contains(pagedResult!.Items, l => l.Id == linkA.Id);
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
