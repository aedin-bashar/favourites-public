using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Dashboard;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;

namespace Favourites.IntegrationTests.Dashboard;

public sealed class DashboardSummaryEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task GetSummary_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_WithNoData_ReturnsAllZeros()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "summary-empty@example.com");

        var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.TotalLinks);
        Assert.Equal(0, body.TotalTags);
        Assert.Equal(0, body.TotalCategories);
        Assert.Equal(0, body.TotalArchived);
        Assert.Equal(0, body.ThisWeek.LinksAdded);
        Assert.Equal(0, body.ThisWeek.CategoriesCreated);
        Assert.Equal(0, body.ThisWeek.TagsCreated);
        Assert.Equal(0, body.ThisWeek.LinksArchived);
    }

    [Fact]
    public async Task GetSummary_CountsAreCorrect()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "summary-counts@example.com");

        // Create 2 active links, 1 archived link
        var link1 = await CreateLinkAsync(client, "https://example.com/one", "One");
        var link2 = await CreateLinkAsync(client, "https://example.com/two", "Two");
        var link3 = await CreateLinkAsync(client, "https://example.com/three", "Three");
        await client.PostAsync($"/api/links/{link3.Id}/archive", null);

        // Create 2 tags
        await CreateTagAsync(client, "alpha");
        await CreateTagAsync(client, "beta");

        // Create 1 category
        await CreateCategoryAsync(client, "Work");

        var response = await client.GetAsync("/api/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.TotalLinks);       // active links only
        Assert.Equal(2, body.TotalTags);
        Assert.Equal(1, body.TotalCategories);
        Assert.Equal(1, body.TotalArchived);
        // This week — all created now so they should be counted
        Assert.Equal(2, body.ThisWeek.LinksAdded);
        Assert.Equal(1, body.ThisWeek.CategoriesCreated);
        Assert.Equal(2, body.ThisWeek.TagsCreated);
        Assert.Equal(1, body.ThisWeek.LinksArchived);
    }

    [Fact]
    public async Task GetSummary_IsUserScoped_DoesNotReturnOtherUsersData()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "summary-scope-a@example.com");
        await CreateLinkAsync(clientA, "https://example.com/a", "A");
        await CreateTagAsync(clientA, "tagA");
        await CreateCategoryAsync(clientA, "CatA");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "summary-scope-b@example.com");

        var response = await clientB.GetAsync("/api/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.TotalLinks);
        Assert.Equal(0, body.TotalTags);
        Assert.Equal(0, body.TotalCategories);
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

    private static async Task CreateTagAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/tags",
            new CreateTagRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
