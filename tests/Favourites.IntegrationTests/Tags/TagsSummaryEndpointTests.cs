using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;

namespace Favourites.IntegrationTests.Tags;

public sealed class TagsSummaryEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task GetTagsSummary_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTagsSummary_WithNoData_ReturnsAllZeros()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tags-summary-empty@example.com");

        var response = await client.GetAsync("/api/tags/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagsSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.TotalTags);
        Assert.Equal(0, body.UnusedTags);
        Assert.Null(body.MostUsed);
        Assert.Null(body.RecentlyAdded);
        Assert.Equal(0, body.PossibleDuplicates);
    }

    [Fact]
    public async Task GetTagsSummary_CountsAreCorrect()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "tags-summary-counts@example.com");

        // Create 3 tags
        var tagA = await CreateTagAsync(client, "Alpha");
        var tagB = await CreateTagAsync(client, "Beta");
        var tagC = await CreateTagAsync(client, "Gamma");

        // Create a link and assign tagA to it (so tagA is used, tagB and tagC are unused)
        var link = await CreateLinkAsync(client, "https://example.com/tags-summary", "Example");
        await client.PutAsJsonAsync($"/api/links/{link.Id}",
            new UpdateLinkRequest("https://example.com/tags-summary", "Example", null, new[] { tagA.Id }, null));

        var response = await client.GetAsync("/api/tags/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagsSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.TotalTags);
        Assert.Equal(2, body.UnusedTags);  // tagB and tagC are unused
        Assert.NotNull(body.MostUsed);
        Assert.Equal(tagA.Id, body.MostUsed!.Id);
        Assert.Equal(1, body.MostUsed.Count);
        Assert.NotNull(body.RecentlyAdded); // most recently created tag
        Assert.Equal(0, body.PossibleDuplicates);
    }

    [Fact]
    public async Task GetTagsSummary_IsUserScoped_DoesNotReturnOtherUsersData()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "tags-summary-scope-a@example.com");
        await CreateTagAsync(clientA, "UserATag");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "tags-summary-scope-b@example.com");

        var response = await clientB.GetAsync("/api/tags/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagsSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.TotalTags);
        Assert.Equal(0, body.UnusedTags);
        Assert.Null(body.MostUsed);
        Assert.Null(body.RecentlyAdded);
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

    private static async Task<TagResponse> CreateTagAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(body);
        return body!;
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
