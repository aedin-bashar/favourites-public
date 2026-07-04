using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Links;

public sealed class ArchiveLinkEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task ArchiveLink_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/links/{Guid.NewGuid()}/archive", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RestoreLink_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/links/{Guid.NewGuid()}/restore", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveLink_WithOwnedLink_PersistsArchivedStateAndHidesFromActiveList()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "archive-owned@example.com");
        var link = await CreateLinkAsync(client, "https://example.com/archive-owned", "Archive Owned");

        var response = await client.PostAsync($"/api/links/{link.Id}/archive", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleAsync(item => item.Id == link.Id);

        Assert.True(stored.IsArchived);
        Assert.NotNull(stored.UpdatedAtUtc);

        var activeResponse = await client.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        var activeBody = await activeResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(activeBody);
        Assert.DoesNotContain(activeBody!.Items, item => item.Id == link.Id);

        var archivedResponse = await client.GetAsync("/api/links?archived=archived");
        Assert.Equal(HttpStatusCode.OK, archivedResponse.StatusCode);
        var archivedBody = await archivedResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(archivedBody);
        var archivedLink = Assert.Single(archivedBody!.Items, item => item.Id == link.Id);
        Assert.True(archivedLink.IsArchived);
    }

    [Fact]
    public async Task RestoreLink_WithOwnedArchivedLink_PersistsActiveStateAndReturnsToActiveList()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "restore-owned@example.com");
        var link = await CreateLinkAsync(client, "https://example.com/restore-owned", "Restore Owned");

        var archiveResponse = await client.PostAsync($"/api/links/{link.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var response = await client.PostAsync($"/api/links/{link.Id}/restore", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleAsync(item => item.Id == link.Id);

        Assert.False(stored.IsArchived);
        Assert.NotNull(stored.UpdatedAtUtc);

        var activeResponse = await client.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        var activeBody = await activeResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(activeBody);
        var activeLink = Assert.Single(activeBody!.Items, item => item.Id == link.Id);
        Assert.False(activeLink.IsArchived);

        var archivedResponse = await client.GetAsync("/api/links?archived=archived");
        Assert.Equal(HttpStatusCode.OK, archivedResponse.StatusCode);
        var archivedBody = await archivedResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(archivedBody);
        Assert.DoesNotContain(archivedBody!.Items, item => item.Id == link.Id);
    }

    [Fact]
    public async Task ArchiveAndRestore_AreIdempotentForOwnedLinks()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "archive-idempotent@example.com");
        var link = await CreateLinkAsync(client, "https://example.com/idempotent", "Idempotent");

        var firstArchive = await client.PostAsync($"/api/links/{link.Id}/archive", null);
        var secondArchive = await client.PostAsync($"/api/links/{link.Id}/archive", null);
        var firstRestore = await client.PostAsync($"/api/links/{link.Id}/restore", null);
        var secondRestore = await client.PostAsync($"/api/links/{link.Id}/restore", null);

        Assert.Equal(HttpStatusCode.NoContent, firstArchive.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondArchive.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, firstRestore.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondRestore.StatusCode);
    }

    [Fact]
    public async Task ArchiveAndRestore_WithAnotherUsersLink_ReturnNotFound()
    {
        await using var factory = new FavouritesApiFactory();

        using var ownerClient = factory.CreateClient();
        await RegisterAndLoginAsync(ownerClient, "archive-cross-owner@example.com");
        var ownerLink = await CreateLinkAsync(
            ownerClient,
            "https://example.com/cross-owner",
            "Cross Owner");

        using var otherClient = factory.CreateClient();
        await RegisterAndLoginAsync(otherClient, "archive-cross-other@example.com");

        var archiveResponse = await otherClient.PostAsync($"/api/links/{ownerLink.Id}/archive", null);
        var restoreResponse = await otherClient.PostAsync($"/api/links/{ownerLink.Id}/restore", null);

        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restoreResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleAsync(link => link.Id == ownerLink.Id);

        Assert.False(stored.IsArchived);
    }

    [Fact]
    public async Task ArchiveAndRestore_WithUnknownLink_ReturnNotFound()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "archive-unknown@example.com");

        var archiveResponse = await client.PostAsync($"/api/links/{Guid.NewGuid()}/archive", null);
        var restoreResponse = await client.PostAsync($"/api/links/{Guid.NewGuid()}/restore", null);

        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restoreResponse.StatusCode);
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
