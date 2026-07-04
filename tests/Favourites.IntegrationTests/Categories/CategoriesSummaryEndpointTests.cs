using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Links;

namespace Favourites.IntegrationTests.Categories;

public sealed class CategoriesSummaryEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task GetCategoriesSummary_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/categories/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCategoriesSummary_WithNoData_ReturnsAllZeros()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "cat-summary-empty@example.com");

        var response = await client.GetAsync("/api/categories/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CategoriesSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.TotalCategories);
        Assert.Equal(0, body.EmptyCategories);
        Assert.Null(body.LargestCategory);
        Assert.Null(body.RecentlyAdded);
    }

    [Fact]
    public async Task GetCategoriesSummary_CountsAreCorrect()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "cat-summary-counts@example.com");

        // Create 3 categories
        var catA = await CreateCategoryAsync(client, "Work");
        var catB = await CreateCategoryAsync(client, "Personal");
        var catC = await CreateCategoryAsync(client, "Archive");

        // Assign 2 links to catA and 1 link to catB — catC stays empty
        await CreateLinkAsync(client, "https://example.com/1", "Link 1", catA.Id);
        await CreateLinkAsync(client, "https://example.com/2", "Link 2", catA.Id);
        await CreateLinkAsync(client, "https://example.com/3", "Link 3", catB.Id);

        var response = await client.GetAsync("/api/categories/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CategoriesSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.TotalCategories);
        Assert.Equal(1, body.EmptyCategories); // catC has no links
        Assert.NotNull(body.LargestCategory);
        Assert.Equal(catA.Id, body.LargestCategory!.Id);
        Assert.Equal(2, body.LargestCategory.Count);
        Assert.NotNull(body.RecentlyAdded); // most recently created category
        Assert.Equal(catC.Id, body.RecentlyAdded!.Id); // catC was created last
    }

    [Fact]
    public async Task GetCategoriesSummary_IsUserScoped_DoesNotReturnOtherUsersData()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "cat-summary-scope-a@example.com");
        var catA = await CreateCategoryAsync(clientA, "UserACategory");
        await CreateLinkAsync(clientA, "https://example.com/a", "Link A", catA.Id);

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "cat-summary-scope-b@example.com");

        var response = await clientB.GetAsync("/api/categories/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CategoriesSummaryResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.TotalCategories);
        Assert.Equal(0, body.EmptyCategories);
        Assert.Null(body.LargestCategory);
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

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(body);
        return body!;
    }

    private static async Task<LinkResponse> CreateLinkAsync(
        HttpClient client, string url, string title, Guid categoryId)
    {
        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(link);

        // Assign the category via update
        var updateResponse = await client.PutAsJsonAsync($"/api/links/{link!.Id}",
            new UpdateLinkRequest(url, title, null, Array.Empty<Guid>(), categoryId));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        return link;
    }
}
