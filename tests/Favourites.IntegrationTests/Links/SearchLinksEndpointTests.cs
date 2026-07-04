using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;

namespace Favourites.IntegrationTests.Links;

public sealed class SearchLinksEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task SearchLinks_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/links?search=anything");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchLinks_MatchesTitleCaseInsensitively()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "title-search@example.com");

        var angular = await CreateLinkAsync(client, "https://example.com/a", "Angular tips and tricks");
        await CreateLinkAsync(client, "https://example.com/b", "React tutorial");

        var response = await client.GetAsync("/api/links?search=angular");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal(angular.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task SearchLinks_MatchesDescription()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "description-search@example.com");

        var match = await CreateLinkAsync(
            client,
            "https://example.com/m",
            "Some random title",
            "An article about EntityFramework Core migrations.");
        await CreateLinkAsync(client, "https://example.com/n", "Unrelated link", "Nothing matching here.");

        var response = await client.GetAsync("/api/links?search=entityframework");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal(match.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task SearchLinks_MatchesUrl()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "url-search@example.com");

        var match = await CreateLinkAsync(client, "https://learn.microsoft.com/dotnet", "Docs", null);
        await CreateLinkAsync(client, "https://example.com/other", "Other", null);

        var response = await client.GetAsync("/api/links?search=microsoft");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal(match.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task SearchLinks_WithNoMatches_ReturnsEmptyList()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "no-match@example.com");
        await CreateLinkAsync(client, "https://example.com/a", "Angular tips", null);
        await CreateLinkAsync(client, "https://example.com/b", "React tutorial", null);

        var response = await client.GetAsync("/api/links?search=something-that-cannot-match");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task SearchLinks_OnlyReturnsAuthenticatedUsersMatches()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "search-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "search-b@example.com");

        var aMatch = await CreateLinkAsync(clientA, "https://example.com/a", "Shared topic for A", null);
        var bMatch = await CreateLinkAsync(clientB, "https://example.com/b", "Shared topic for B", null);

        var aResponse = await clientA.GetAsync("/api/links?search=shared");
        Assert.Equal(HttpStatusCode.OK, aResponse.StatusCode);
        var aBody = await aResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(aBody);
        Assert.Single(aBody!.Items);
        Assert.Equal(aMatch.Id, aBody.Items[0].Id);
        Assert.DoesNotContain(bMatch.Id, aBody.Items.Select(l => l.Id));

        var bResponse = await clientB.GetAsync("/api/links?search=shared");
        Assert.Equal(HttpStatusCode.OK, bResponse.StatusCode);
        var bBody = await bResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(bBody);
        Assert.Single(bBody!.Items);
        Assert.Equal(bMatch.Id, bBody.Items[0].Id);
        Assert.DoesNotContain(aMatch.Id, bBody.Items.Select(l => l.Id));
    }

    [Fact]
    public async Task SearchLinks_WithEmptyOrWhitespaceQuery_ReturnsAllLinks()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "empty-search@example.com");
        await CreateLinkAsync(client, "https://example.com/a", "First", null);
        await CreateLinkAsync(client, "https://example.com/b", "Second", null);

        var emptyResponse = await client.GetAsync("/api/links?search=");
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        var emptyBody = await emptyResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(emptyBody);
        Assert.Equal(2, emptyBody!.Items.Count);

        var whitespaceResponse = await client.GetAsync("/api/links?search=%20%20%20");
        Assert.Equal(HttpStatusCode.OK, whitespaceResponse.StatusCode);
        var whitespaceBody = await whitespaceResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(whitespaceBody);
        Assert.Equal(2, whitespaceBody!.Items.Count);
    }

    [Fact]
    public async Task SearchLinks_PreservesNewestFirstOrdering()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "ordered-search@example.com");

        var first = await CreateLinkAsync(client, "https://example.com/1", "Topic alpha post", null);
        await Task.Delay(20);
        var second = await CreateLinkAsync(client, "https://example.com/2", "Topic alpha follow-up", null);
        await Task.Delay(20);
        var third = await CreateLinkAsync(client, "https://example.com/3", "Topic alpha summary", null);

        var response = await client.GetAsync("/api/links?search=alpha");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Items.Count);
        Assert.Equal(third.Id, body.Items[0].Id);
        Assert.Equal(second.Id, body.Items[1].Id);
        Assert.Equal(first.Id, body.Items[2].Id);
    }

    private static async Task<LinkResponse> CreateLinkAsync(
        HttpClient client,
        string url,
        string title,
        string? description = null)
    {
        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, description));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
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
