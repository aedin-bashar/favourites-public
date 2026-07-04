using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;

namespace Favourites.IntegrationTests.Links;

public sealed class TagFilteringEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task FilterByTag_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/links?tagId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FilterByTag_ReturnsOnlyLinksWithThatTag()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tag-filter@example.com");

        var research = await CreateTagAsync(client, "research");
        var dotnet = await CreateTagAsync(client, "dotnet");

        var taggedWithResearch = await CreateLinkAsync(
            client,
            "https://example.com/research",
            "Research link",
            tagIds: new[] { research.Id });

        await CreateLinkAsync(
            client,
            "https://example.com/dotnet",
            "Dotnet link",
            tagIds: new[] { dotnet.Id });

        await CreateLinkAsync(client, "https://example.com/none", "Untagged link");

        var response = await client.GetAsync($"/api/links?tagId={research.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();

        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal(taggedWithResearch.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task FilterByTag_WithAnotherUsersTagId_ReturnsEmpty()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "tag-filter-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "tag-filter-b@example.com");

        var bTag = await CreateTagAsync(clientB, "private");
        await CreateLinkAsync(
            clientB,
            "https://example.com/b-link",
            "B link",
            tagIds: new[] { bTag.Id });

        var response = await clientA.GetAsync($"/api/links?tagId={bTag.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();

        Assert.NotNull(body);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task FilterByTag_WithUnknownTagId_ReturnsEmpty()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tag-filter-unknown@example.com");
        await CreateLinkAsync(client, "https://example.com/a", "A");

        var response = await client.GetAsync($"/api/links?tagId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();

        Assert.NotNull(body);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task FilterByTag_CombinedWithSearch_AppliesBothFilters()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tag-filter-combined@example.com");

        var dotnet = await CreateTagAsync(client, "dotnet");

        var match = await CreateLinkAsync(
            client,
            "https://example.com/aspnetcore",
            "ASP.NET Core tips",
            tagIds: new[] { dotnet.Id });

        await CreateLinkAsync(
            client,
            "https://example.com/other",
            "Other tagged link",
            tagIds: new[] { dotnet.Id });

        await CreateLinkAsync(
            client,
            "https://example.com/notag",
            "ASP.NET Core untagged",
            tagIds: null);

        var response = await client.GetAsync($"/api/links?tagId={dotnet.Id}&search=ASP.NET");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();

        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal(match.Id, body.Items[0].Id);
    }

    private static async Task<LinkResponse> CreateLinkAsync(
        HttpClient client,
        string url,
        string title,
        string? description = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        Guid? categoryId = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/links",
            new CreateLinkRequest(url, title, description, tagIds, categoryId));

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
