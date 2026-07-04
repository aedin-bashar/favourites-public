using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;
using Favourites.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Links;

public sealed class CreateLinkEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task CreateLink_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com", "Example", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateLink_WithValidRequest_ReturnsCreatedWithLinkData()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "create-success@example.com");

        var request = new CreateLinkRequest(
            Url: "https://example.com/articles/first",
            Title: "  First Example  ",
            Description: "  A useful article.  ");

        var response = await client.PostAsJsonAsync("/api/links", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();

        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal("https://example.com/articles/first", body.Url);
        Assert.Equal("First Example", body.Title);
        Assert.Equal("A useful article.", body.Description);
        Assert.False(body.IsArchived);
        Assert.NotEqual(default, body.CreatedAtUtc);
        Assert.Null(body.UpdatedAtUtc);

        Assert.Equal($"/api/links/{body.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreateLink_WithValidRequest_PersistsLinkInDatabase()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "persist-success@example.com");

        var request = new CreateLinkRequest(
            Url: "https://example.com/persist",
            Title: "Persist Me",
            Description: null);

        var response = await client.PostAsJsonAsync("/api/links", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(body);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Id == body!.Id);

        Assert.NotNull(stored);
        Assert.Equal("https://example.com/persist", stored!.Url);
        Assert.Equal("Persist Me", stored.Title);
        Assert.Null(stored.Description);
        Assert.False(stored.IsArchived);
    }

    [Fact]
    public async Task CreateLink_WithOwnedTags_ReturnsAndPersistsSelectedTags()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "create-tags@example.com");

        var research = await CreateTagAsync(client, "research");
        var dotnet = await CreateTagAsync(client, "dotnet");

        var request = new CreateLinkRequest(
            Url: "https://example.com/tagged",
            Title: "Tagged link",
            Description: null,
            TagIds: new[] { research.Id, dotnet.Id });

        var response = await client.PostAsJsonAsync("/api/links", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();

        Assert.NotNull(body);
        Assert.Equal(new[] { dotnet.Id, research.Id }, body!.Tags.Select(tag => tag.Id));
        Assert.Equal(new[] { "dotnet", "research" }, body.Tags.Select(tag => tag.Name));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var storedTagIds = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(linkTag => linkTag.FavouriteLinkId == body.Id)
            .Select(linkTag => linkTag.TagId)
            .ToListAsync();

        Assert.Equal(2, storedTagIds.Count);
        Assert.Contains(research.Id, storedTagIds);
        Assert.Contains(dotnet.Id, storedTagIds);
    }

    [Fact]
    public async Task CreateLink_WithAnotherUsersTag_ReturnsValidationError()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "create-cross-tag-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "create-cross-tag-b@example.com");

        var bTag = await CreateTagAsync(clientB, "private");

        var response = await clientA.PostAsJsonAsync(
            "/api/links",
            new CreateLinkRequest(
                Url: "https://example.com/cross-tag",
                Title: "Cross tag",
                Description: null,
                TagIds: new[] { bTag.Id }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLinkRequest.TagIds), problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("", "Valid Title", null, nameof(CreateLinkRequest.Url))]
    [InlineData("   ", "Valid Title", null, nameof(CreateLinkRequest.Url))]
    [InlineData("not-a-url", "Valid Title", null, nameof(CreateLinkRequest.Url))]
    [InlineData("ftp://example.com", "Valid Title", null, nameof(CreateLinkRequest.Url))]
    [InlineData("/relative/path", "Valid Title", null, nameof(CreateLinkRequest.Url))]
    [InlineData("https://example.com", "", null, nameof(CreateLinkRequest.Title))]
    [InlineData("https://example.com", "   ", null, nameof(CreateLinkRequest.Title))]
    public async Task CreateLink_WithInvalidField_ReturnsValidationError(
        string url,
        string title,
        string? description,
        string expectedErrorKey)
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, $"invalid-{Guid.NewGuid():N}@example.com");

        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, description));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(expectedErrorKey, problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLink_WithOversizedTitle_ReturnsValidationError()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "oversized-title@example.com");

        var oversizedTitle = new string('a', 201);

        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com", oversizedTitle, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLinkRequest.Title), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLink_WithOversizedDescription_ReturnsValidationError()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "oversized-description@example.com");

        var oversizedDescription = new string('a', 2001);

        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com", "Title", oversizedDescription));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateLinkRequest.Description), problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateLink_AssignsAuthenticatedUserAsOwner_NotAnyValueFromClient()
    {
        await using var factory = new FavouritesApiFactory();

        // User A — the authenticated owner.
        using var clientA = factory.CreateClient();
        var userAId = await RegisterAndLoginAsync(clientA, "owner-a@example.com");

        // User B — another registered user. The link must NOT be assigned to user B
        // even if a malicious client tries to send userId in the body.
        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "owner-b@example.com");

        Assert.NotEqual(userAId, userBId);

        // Send a body that includes a UserId field the API does NOT model.
        // The contract type ignores it; the backend resolves UserId from the auth cookie only.
        var maliciousBody = new
        {
            Url = "https://example.com/ownership-test",
            Title = "Ownership Test",
            Description = (string?)null,
            UserId = userBId
        };

        var response = await clientA.PostAsJsonAsync("/api/links", maliciousBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(body);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Id == body!.Id);

        Assert.NotNull(stored);
        Assert.Equal(userAId, stored!.UserId);
        Assert.NotEqual(userBId, stored.UserId);
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

    private static async Task<TagResponse> CreateTagAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest(name));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(body);
        return body!;
    }
}
