using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Tags;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Tags;

public sealed class TagOwnershipEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task GetUserTags_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTag_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("reading"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTag_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/tags/{Guid.NewGuid()}",
            new UpdateTagRequest("reading"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTag_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/tags/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserTags_ReturnsOnlyAuthenticatedUsersTags()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "tags-list-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "tags-list-b@example.com");

        var aFirst = await CreateTagAsync(clientA, "alpha");
        var aSecond = await CreateTagAsync(clientA, "beta");
        var bOnly = await CreateTagAsync(clientB, "gamma");

        var aResponse = await clientA.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.OK, aResponse.StatusCode);
        var aPaged = await aResponse.Content.ReadFromJsonAsync<PagedTagsResponse>();
        Assert.NotNull(aPaged);
        Assert.Equal(2, aPaged!.Items.Count);

        var aIds = aPaged.Items.Select(tag => tag.Id).ToHashSet();
        Assert.Contains(aFirst.Id, aIds);
        Assert.Contains(aSecond.Id, aIds);
        Assert.DoesNotContain(bOnly.Id, aIds);

        var bResponse = await clientB.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.OK, bResponse.StatusCode);
        var bPaged = await bResponse.Content.ReadFromJsonAsync<PagedTagsResponse>();
        Assert.NotNull(bPaged);
        Assert.Single(bPaged!.Items);
        Assert.Equal(bOnly.Id, bPaged.Items[0].Id);
    }

    [Fact]
    public async Task CreateTag_AssignsAuthenticatedUserAsOwner_NotAnyValueFromClient()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        var userAId = await RegisterAndLoginAsync(clientA, "tags-create-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "tags-create-b@example.com");

        var maliciousBody = new
        {
            Name = "  productivity  ",
            UserId = userBId
        };

        var response = await clientA.PostAsJsonAsync("/api/tags", maliciousBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal("productivity", body.Name);
        Assert.Equal($"/api/tags/{body.Id}", response.Headers.Location?.ToString());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Tags.AsNoTracking()
            .SingleOrDefaultAsync(tag => tag.Id == body.Id);

        Assert.NotNull(stored);
        Assert.Equal(userAId, stored!.UserId);
        Assert.NotEqual(userBId, stored.UserId);
    }

    [Fact]
    public async Task UpdateTag_WithOwnedTag_ReturnsOkAndPersistsNewName()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tags-update-own@example.com");

        var created = await CreateTagAsync(client, "reading");

        var response = await client.PutAsJsonAsync(
            $"/api/tags/{created.Id}",
            new UpdateTagRequest("  dotnet  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("dotnet", body.Name);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Tags.AsNoTracking()
            .SingleOrDefaultAsync(tag => tag.Id == created.Id);

        Assert.NotNull(stored);
        Assert.Equal("dotnet", stored!.Name);
    }

    [Fact]
    public async Task UpdateTag_WhenTagBelongsToAnotherUser_ReturnsNotFoundAndLeavesTagUnchanged()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "tags-update-cross-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "tags-update-cross-b@example.com");

        var bTag = await CreateTagAsync(clientB, "private");

        var response = await clientA.PutAsJsonAsync(
            $"/api/tags/{bTag.Id}",
            new UpdateTagRequest("hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Tags.AsNoTracking()
            .SingleOrDefaultAsync(tag => tag.Id == bTag.Id);

        Assert.NotNull(stored);
        Assert.Equal(userBId, stored!.UserId);
        Assert.Equal("private", stored.Name);
    }

    [Fact]
    public async Task DeleteTag_WithOwnedTag_ReturnsNoContentAndRemovesRow()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tags-delete-own@example.com");

        var created = await CreateTagAsync(client, "temporary");

        var response = await client.DeleteAsync($"/api/tags/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Tags.AsNoTracking()
            .SingleOrDefaultAsync(tag => tag.Id == created.Id);

        Assert.Null(stored);
    }

    [Fact]
    public async Task DeleteTag_WhenTagBelongsToAnotherUser_ReturnsNotFoundAndLeavesTagUnchanged()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "tags-delete-cross-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "tags-delete-cross-b@example.com");

        var bTag = await CreateTagAsync(clientB, "keep-me");

        var response = await clientA.DeleteAsync($"/api/tags/{bTag.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Tags.AsNoTracking()
            .SingleOrDefaultAsync(tag => tag.Id == bTag.Id);

        Assert.NotNull(stored);
        Assert.Equal(userBId, stored!.UserId);
        Assert.Equal("keep-me", stored.Name);
    }

    private static async Task<TagResponse> CreateTagAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest(name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
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
