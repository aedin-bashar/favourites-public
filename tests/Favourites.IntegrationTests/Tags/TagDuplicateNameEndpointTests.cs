using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Tags;
using Favourites.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Tags;

public sealed class TagDuplicateNameEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task CreateTag_WithDuplicateNameForSameUser_ReturnsValidationError()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tags-duplicate-create@example.com");
        var created = await CreateTagAsync(client, "reading");

        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("  READING  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateTagRequest.Name), problem!.Errors.Keys);

        var listResponse = await client.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var paged = await listResponse.Content.ReadFromJsonAsync<PagedTagsResponse>();
        Assert.NotNull(paged);
        Assert.Single(paged!.Items);
        Assert.Equal(created.Id, paged.Items[0].Id);
    }

    [Fact]
    public async Task CreateTag_AllowsSameNameForDifferentUsers()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "tags-duplicate-create-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "tags-duplicate-create-b@example.com");

        var aTag = await CreateTagAsync(clientA, "reading");
        var bTag = await CreateTagAsync(clientB, "reading");

        Assert.NotEqual(aTag.Id, bTag.Id);
        Assert.Equal(aTag.Name, bTag.Name);
    }

    [Fact]
    public async Task UpdateTag_WithDuplicateNameForSameUser_ReturnsValidationErrorAndLeavesTagUnchanged()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tags-duplicate-update@example.com");
        await CreateTagAsync(client, "reading");
        var dotnet = await CreateTagAsync(client, "dotnet");

        var response = await client.PutAsJsonAsync(
            $"/api/tags/{dotnet.Id}",
            new UpdateTagRequest("  READING  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(UpdateTagRequest.Name), problem!.Errors.Keys);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Tags.AsNoTracking()
            .SingleOrDefaultAsync(tag => tag.Id == dotnet.Id);

        Assert.NotNull(stored);
        Assert.Equal("dotnet", stored!.Name);
    }

    [Fact]
    public async Task UpdateTag_AllowsKeepingSameNameForSameTag()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tags-duplicate-update-same@example.com");
        var created = await CreateTagAsync(client, "reading");

        var response = await client.PutAsJsonAsync(
            $"/api/tags/{created.Id}",
            new UpdateTagRequest("  reading  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("reading", body.Name);
    }

    [Fact]
    public async Task UpdateTag_AllowsNameUsedByAnotherUser()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "tags-duplicate-update-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "tags-duplicate-update-b@example.com");

        await CreateTagAsync(clientA, "reading");
        var bTag = await CreateTagAsync(clientB, "dotnet");

        var response = await clientB.PutAsJsonAsync(
            $"/api/tags/{bTag.Id}",
            new UpdateTagRequest("reading"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(body);
        Assert.Equal(bTag.Id, body!.Id);
        Assert.Equal("reading", body.Name);
    }

    private static async Task<TagResponse> CreateTagAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest(name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(body);
        return body!;
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
}
