using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Links;

public sealed class DeleteLinkEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task DeleteLink_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/links/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Task 2.56: users CAN delete their own links --------------------

    [Fact]
    public async Task DeleteLink_WithOwnedLink_ReturnsNoContentAndRemovesRow()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "delete-own@example.com");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/will-be-deleted",
            "To delete",
            "Goodbye.");

        var response = await client.DeleteAsync($"/api/links/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Id == created.Id);

        Assert.Null(stored);
    }

    [Fact]
    public async Task DeleteLink_AfterDelete_GetByIdReturnsNotFound()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "delete-then-get@example.com");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/now-you-see-me",
            "Vanishing",
            null);

        var deleteResponse = await client.DeleteAsync($"/api/links/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/links/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_OnlyRemovesTargetedLink()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "delete-isolated@example.com");

        var keep = await CreateLinkAsync(
            client,
            "https://example.com/keep",
            "Keep me",
            null);

        var remove = await CreateLinkAsync(
            client,
            "https://example.com/remove",
            "Remove me",
            null);

        var response = await client.DeleteAsync($"/api/links/{remove.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var keepStill = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Id == keep.Id);
        var removed = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Id == remove.Id);

        Assert.NotNull(keepStill);
        Assert.Null(removed);
    }

    [Fact]
    public async Task DeleteLink_WithUnknownId_ReturnsNotFound()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "delete-unknown@example.com");

        var response = await client.DeleteAsync($"/api/links/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Task 2.57: users CANNOT delete another user's link -------------

    [Fact]
    public async Task DeleteLink_WhenLinkBelongsToAnotherUser_ReturnsNotFoundAndLeavesLinkUnchanged()
    {
        // Ownership-not-found rule (architecture invariant): cross-user
        // deletes must return 404 (same as a missing id), never 403, so
        // attackers can't enumerate other users' link ids by status code.
        // The target link must also remain in the database.
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "delete-cross-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "delete-cross-b@example.com");

        var bLink = await CreateLinkAsync(
            clientB,
            "https://example.com/b-untouchable",
            "B's link",
            "Hands off.");

        // User A tries to delete user B's link.
        var response = await clientA.DeleteAsync($"/api/links/{bLink.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // B's link must still exist, byte-for-byte unchanged.
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Id == bLink.Id);

        Assert.NotNull(stored);
        Assert.Equal(userBId, stored!.UserId);
        Assert.Equal(bLink.Url, stored.Url);
        Assert.Equal(bLink.Title, stored.Title);
        Assert.Equal(bLink.Description, stored.Description);

        // B can still read the link via the API.
        var bRead = await clientB.GetAsync($"/api/links/{bLink.Id}");
        Assert.Equal(HttpStatusCode.OK, bRead.StatusCode);
    }

    private static async Task<LinkResponse> CreateLinkAsync(
        HttpClient client,
        string url,
        string title,
        string? description)
    {
        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, description));

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
