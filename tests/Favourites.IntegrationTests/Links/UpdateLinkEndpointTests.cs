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

public sealed class UpdateLinkEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task UpdateLink_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/links/{Guid.NewGuid()}",
            new UpdateLinkRequest("https://example.com/x", "X", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Task 2.50: users CAN update their own links --------------------

    [Fact]
    public async Task UpdateLink_WithOwnedLink_ReturnsOkAndUpdatesFields()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "update-own@example.com");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/before",
            "Before Title",
            "Before description.");

        // Small delay so UpdatedAtUtc is strictly later than CreatedAtUtc
        // even on fast machines.
        await Task.Delay(20);

        var update = new UpdateLinkRequest(
            Url: "https://example.com/after",
            Title: "  After Title  ",
            Description: "  After description.  ");

        var response = await client.PutAsJsonAsync($"/api/links/{created.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();

        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("https://example.com/after", body.Url);
        Assert.Equal("After Title", body.Title);
        Assert.Equal("After description.", body.Description);
        Assert.False(body.IsArchived);
        Assert.Equal(created.CreatedAtUtc, body.CreatedAtUtc);
        Assert.NotNull(body.UpdatedAtUtc);
        Assert.True(body.UpdatedAtUtc > created.CreatedAtUtc);
    }

    [Fact]
    public async Task UpdateLink_WithOwnedLink_PersistsNewValuesInDatabase()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "update-persist@example.com");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/persist-before",
            "Persist Before",
            null);

        var update = new UpdateLinkRequest(
            Url: "https://example.com/persist-after",
            Title: "Persist After",
            Description: "Now described.");

        var response = await client.PutAsJsonAsync($"/api/links/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Id == created.Id);

        Assert.NotNull(stored);
        Assert.Equal("https://example.com/persist-after", stored!.Url);
        Assert.Equal("Persist After", stored.Title);
        Assert.Equal("Now described.", stored.Description);
        Assert.NotNull(stored.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateLink_CanClearDescription_BySendingNull()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "update-clear-desc@example.com");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/with-desc",
            "Has description",
            "Will be cleared.");

        var update = new UpdateLinkRequest(
            Url: created.Url,
            Title: created.Title,
            Description: null);

        var response = await client.PutAsJsonAsync($"/api/links/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.Description);
    }

    [Fact]
    public async Task UpdateLink_WithOwnedTags_ReplacesSelectedTags()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "update-tags@example.com");

        var oldTag = await CreateTagAsync(client, "old");
        var newTag = await CreateTagAsync(client, "new");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/tagged-before",
            "Tagged Before",
            null,
            new[] { oldTag.Id });

        var update = new UpdateLinkRequest(
            Url: "https://example.com/tagged-after",
            Title: "Tagged After",
            Description: null,
            TagIds: new[] { newTag.Id });

        var response = await client.PutAsJsonAsync($"/api/links/{created.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();

        Assert.NotNull(body);
        Assert.Single(body!.Tags);
        Assert.Equal(newTag.Id, body.Tags[0].Id);
        Assert.Equal("new", body.Tags[0].Name);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var storedTagIds = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(linkTag => linkTag.FavouriteLinkId == created.Id)
            .Select(linkTag => linkTag.TagId)
            .ToListAsync();

        Assert.Single(storedTagIds);
        Assert.Equal(newTag.Id, storedTagIds[0]);
    }

    [Fact]
    public async Task UpdateLink_WhenTagIdsAreOmitted_PreservesExistingTags()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "update-tags-preserve@example.com");

        var tag = await CreateTagAsync(client, "keep");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/keep-tags",
            "Keep Tags",
            null,
            new[] { tag.Id });

        var update = new
        {
            Url = "https://example.com/keep-tags-updated",
            Title = "Keep Tags Updated",
            Description = (string?)null
        };

        var response = await client.PutAsJsonAsync($"/api/links/{created.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();

        Assert.NotNull(body);
        Assert.Single(body!.Tags);
        Assert.Equal(tag.Id, body.Tags[0].Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var storedTagIds = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(linkTag => linkTag.FavouriteLinkId == created.Id)
            .Select(linkTag => linkTag.TagId)
            .ToListAsync();

        Assert.Single(storedTagIds);
        Assert.Equal(tag.Id, storedTagIds[0]);
    }

    [Fact]
    public async Task UpdateLink_WithAnotherUsersTag_ReturnsValidationError()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "update-cross-tag-a@example.com");

        var ownLink = await CreateLinkAsync(
            clientA,
            "https://example.com/own",
            "Own",
            null);

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "update-cross-tag-b@example.com");
        var bTag = await CreateTagAsync(clientB, "private");

        var response = await clientA.PutAsJsonAsync(
            $"/api/links/{ownLink.Id}",
            new UpdateLinkRequest(
                Url: ownLink.Url,
                Title: ownLink.Title,
                Description: ownLink.Description,
                TagIds: new[] { bTag.Id }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(nameof(UpdateLinkRequest.TagIds), problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("", "Valid Title", null, nameof(UpdateLinkRequest.Url))]
    [InlineData("   ", "Valid Title", null, nameof(UpdateLinkRequest.Url))]
    [InlineData("not-a-url", "Valid Title", null, nameof(UpdateLinkRequest.Url))]
    [InlineData("ftp://example.com", "Valid Title", null, nameof(UpdateLinkRequest.Url))]
    [InlineData("/relative/path", "Valid Title", null, nameof(UpdateLinkRequest.Url))]
    [InlineData("https://example.com", "", null, nameof(UpdateLinkRequest.Title))]
    [InlineData("https://example.com", "   ", null, nameof(UpdateLinkRequest.Title))]
    public async Task UpdateLink_WithInvalidField_ReturnsValidationError(
        string url,
        string title,
        string? description,
        string expectedErrorKey)
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, $"update-invalid-{Guid.NewGuid():N}@example.com");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/will-not-change",
            "Original",
            null);

        var response = await client.PutAsJsonAsync(
            $"/api/links/{created.Id}",
            new UpdateLinkRequest(url, title, description));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(expectedErrorKey, problem!.Errors.Keys);
    }

    [Fact]
    public async Task UpdateLink_WithUnknownId_ReturnsNotFound()
    {
        // 404 distinguishes "no such id" from validation failures.
        // It is also what cross-user updates return — see the next test.
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "update-unknown@example.com");

        var response = await client.PutAsJsonAsync(
            $"/api/links/{Guid.NewGuid()}",
            new UpdateLinkRequest("https://example.com/x", "X", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Task 2.51: users CANNOT update another user's link -------------

    [Fact]
    public async Task UpdateLink_WhenLinkBelongsToAnotherUser_ReturnsNotFoundAndLeavesLinkUnchanged()
    {
        // Ownership-not-found rule (architecture invariant): cross-user
        // updates must return 404 (same as a missing id), never 403, so
        // attackers can't enumerate other users' link ids by status code.
        // The target link must also remain untouched in the database.
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "update-cross-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "update-cross-b@example.com");

        var bLink = await CreateLinkAsync(
            clientB,
            "https://example.com/b-original",
            "B Original",
            "B's original description.");

        // User A tries to overwrite user B's link.
        var maliciousUpdate = new UpdateLinkRequest(
            Url: "https://example.com/hijacked",
            Title: "Hijacked by A",
            Description: "A's overwrite attempt.");

        var response = await clientA.PutAsJsonAsync($"/api/links/{bLink.Id}", maliciousUpdate);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // B's link must be byte-for-byte unchanged in the database.
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Id == bLink.Id);

        Assert.NotNull(stored);
        Assert.Equal(userBId, stored!.UserId);
        Assert.Equal(bLink.Url, stored.Url);
        Assert.Equal(bLink.Title, stored.Title);
        Assert.Equal(bLink.Description, stored.Description);
        Assert.Null(stored.UpdatedAtUtc);

        // B can still read the link via the API — and it still matches.
        var bRead = await clientB.GetAsync($"/api/links/{bLink.Id}");
        Assert.Equal(HttpStatusCode.OK, bRead.StatusCode);
        var bAfter = await bRead.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(bAfter);
        Assert.Equal(bLink.Url, bAfter!.Url);
        Assert.Equal(bLink.Title, bAfter.Title);
        Assert.Equal(bLink.Description, bAfter.Description);
        Assert.Null(bAfter.UpdatedAtUtc);
    }

    private static async Task<LinkResponse> CreateLinkAsync(
        HttpClient client,
        string url,
        string title,
        string? description,
        IReadOnlyCollection<Guid>? tagIds = null)
    {
        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, description, tagIds));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(body);
        return body!;
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
