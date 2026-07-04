using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Links;

namespace Favourites.IntegrationTests.Links;

public sealed class CategoryFilteringEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task FilterByCategory_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/links?categoryId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FilterByCategory_ReturnsOnlyLinksInThatCategory()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "category-filter@example.com");

        var work = await CreateCategoryAsync(client, "work");
        var personal = await CreateCategoryAsync(client, "personal");

        var workLink = await CreateLinkAsync(
            client,
            "https://example.com/work",
            "Work link",
            categoryId: work.Id);

        await CreateLinkAsync(
            client,
            "https://example.com/personal",
            "Personal link",
            categoryId: personal.Id);

        await CreateLinkAsync(client, "https://example.com/none", "Uncategorised link");

        var response = await client.GetAsync($"/api/links?categoryId={work.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();

        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal(workLink.Id, body.Items[0].Id);
        Assert.NotNull(body.Items[0].Category);
        Assert.Equal(work.Id, body.Items[0].Category!.Id);
    }

    [Fact]
    public async Task FilterByCategory_WithAnotherUsersCategoryId_ReturnsEmpty()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "category-filter-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "category-filter-b@example.com");

        var bCategory = await CreateCategoryAsync(clientB, "private");
        await CreateLinkAsync(
            clientB,
            "https://example.com/b-link",
            "B link",
            categoryId: bCategory.Id);

        var response = await clientA.GetAsync($"/api/links?categoryId={bCategory.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();

        Assert.NotNull(body);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task FilterByCategory_WithUnknownCategoryId_ReturnsEmpty()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "category-filter-unknown@example.com");
        await CreateLinkAsync(client, "https://example.com/a", "A");

        var response = await client.GetAsync($"/api/links?categoryId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();

        Assert.NotNull(body);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task CreateLink_WithAnotherUsersCategory_ReturnsValidationError()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "category-create-cross-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "category-create-cross-b@example.com");

        var bCategory = await CreateCategoryAsync(clientB, "private");

        var response = await clientA.PostAsJsonAsync(
            "/api/links",
            new CreateLinkRequest(
                Url: "https://example.com/cross-category",
                Title: "Cross category",
                Description: null,
                TagIds: null,
                CategoryId: bCategory.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListLinks_IncludesCategoryOnResponseWhenAssigned()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "category-list@example.com");

        var work = await CreateCategoryAsync(client, "work");

        await CreateLinkAsync(
            client,
            "https://example.com/with-category",
            "With category",
            categoryId: work.Id);

        await CreateLinkAsync(client, "https://example.com/without", "Without category");

        var response = await client.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Items.Count);

        var withCategory = body.Items.Single(l => l.Title == "With category");
        var withoutCategory = body.Items.Single(l => l.Title == "Without category");

        Assert.NotNull(withCategory.Category);
        Assert.Equal(work.Id, withCategory.Category!.Id);
        Assert.Equal("work", withCategory.Category.Name);
        Assert.Null(withoutCategory.Category);
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

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
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
