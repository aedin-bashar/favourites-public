using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Links;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Categories;

public sealed class CategoryOwnershipEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task GetUserCategories_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("work"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/categories/{Guid.NewGuid()}",
            new UpdateCategoryRequest("work"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/categories/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserCategories_ReturnsOnlyAuthenticatedUsersCategories()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "categories-list-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "categories-list-b@example.com");

        var aFirst = await CreateCategoryAsync(clientA, "alpha");
        var aSecond = await CreateCategoryAsync(clientA, "beta");
        var bOnly = await CreateCategoryAsync(clientB, "gamma");

        var aResponse = await clientA.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, aResponse.StatusCode);
        var aPaged = await aResponse.Content.ReadFromJsonAsync<PagedCategoriesResponse>();
        Assert.NotNull(aPaged);
        Assert.Equal(2, aPaged!.Items.Count);

        var aIds = aPaged.Items.Select(category => category.Id).ToHashSet();
        Assert.Contains(aFirst.Id, aIds);
        Assert.Contains(aSecond.Id, aIds);
        Assert.DoesNotContain(bOnly.Id, aIds);

        var bResponse = await clientB.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, bResponse.StatusCode);
        var bPaged = await bResponse.Content.ReadFromJsonAsync<PagedCategoriesResponse>();
        Assert.NotNull(bPaged);
        Assert.Single(bPaged!.Items);
        Assert.Equal(bOnly.Id, bPaged.Items[0].Id);
    }

    [Fact]
    public async Task CreateCategory_AssignsAuthenticatedUserAsOwner_NotAnyValueFromClient()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        var userAId = await RegisterAndLoginAsync(clientA, "categories-create-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "categories-create-b@example.com");

        var maliciousBody = new
        {
            Name = "  productivity  ",
            UserId = userBId
        };

        var response = await clientA.PostAsJsonAsync("/api/categories", maliciousBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal("productivity", body.Name);
        Assert.Equal($"/api/categories/{body.Id}", response.Headers.Location?.ToString());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Categories.AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == body.Id);

        Assert.NotNull(stored);
        Assert.Equal(userAId, stored!.UserId);
        Assert.NotEqual(userBId, stored.UserId);
    }

    [Fact]
    public async Task UpdateCategory_WithOwnedCategory_ReturnsOkAndPersistsNewName()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "categories-update-own@example.com");

        var created = await CreateCategoryAsync(client, "reading");

        var response = await client.PutAsJsonAsync(
            $"/api/categories/{created.Id}",
            new UpdateCategoryRequest("  dotnet  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("dotnet", body.Name);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Categories.AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == created.Id);

        Assert.NotNull(stored);
        Assert.Equal("dotnet", stored!.Name);
    }

    [Fact]
    public async Task UpdateCategory_WhenCategoryBelongsToAnotherUser_ReturnsNotFoundAndLeavesCategoryUnchanged()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "categories-update-cross-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "categories-update-cross-b@example.com");

        var bCategory = await CreateCategoryAsync(clientB, "private");

        var response = await clientA.PutAsJsonAsync(
            $"/api/categories/{bCategory.Id}",
            new UpdateCategoryRequest("hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Categories.AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == bCategory.Id);

        Assert.NotNull(stored);
        Assert.Equal(userBId, stored!.UserId);
        Assert.Equal("private", stored.Name);
    }

    [Fact]
    public async Task DeleteCategory_WithOwnedCategory_ReturnsNoContentAndRemovesRow()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "categories-delete-own@example.com");

        var created = await CreateCategoryAsync(client, "temporary");

        var response = await client.DeleteAsync($"/api/categories/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Categories.AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == created.Id);

        Assert.Null(stored);
    }

    [Fact]
    public async Task DeleteCategory_WithAssignedLinks_UnassignsLinksAndRemovesCategory()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "categories-delete-assigned@example.com");

        var created = await CreateCategoryAsync(client, "reference");
        var link = await CreateLinkAsync(client, "https://example.com/reference", "Reference", created.Id);

        var response = await client.DeleteAsync($"/api/categories/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var linkResponse = await client.GetAsync($"/api/links/{link.Id}");
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);

        var linkBody = await linkResponse.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(linkBody);
        Assert.Null(linkBody!.Category);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var storedCategory = await dbContext.Categories.AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == created.Id);
        var storedLink = await dbContext.FavouriteLinks.AsNoTracking()
            .SingleOrDefaultAsync(favouriteLink => favouriteLink.Id == link.Id);

        Assert.Null(storedCategory);
        Assert.NotNull(storedLink);
        Assert.Null(storedLink!.CategoryId);
    }

    [Fact]
    public async Task DeleteCategory_WhenCategoryBelongsToAnotherUser_ReturnsNotFoundAndLeavesCategoryUnchanged()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "categories-delete-cross-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "categories-delete-cross-b@example.com");

        var bCategory = await CreateCategoryAsync(clientB, "keep-me");

        var response = await clientA.DeleteAsync($"/api/categories/{bCategory.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Categories.AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == bCategory.Id);

        Assert.NotNull(stored);
        Assert.Equal(userBId, stored!.UserId);
        Assert.Equal("keep-me", stored.Name);
    }

    [Fact]
    public async Task GetUserCategories_WithPaginationParams_ReturnsCorrectPageAndTotal()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "categories-pagination@example.com");

        await CreateCategoryAsync(client, "alpha");
        await CreateCategoryAsync(client, "beta");
        await CreateCategoryAsync(client, "gamma");

        var page1 = await client.GetAsync("/api/categories?page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        var body1 = await page1.Content.ReadFromJsonAsync<PagedCategoriesResponse>();
        Assert.NotNull(body1);
        Assert.Equal(3, body1!.Total);
        Assert.Equal(2, body1.Items.Count);
        Assert.Equal(1, body1.Page);
        Assert.Equal(2, body1.PageSize);

        var page2 = await client.GetAsync("/api/categories?page=2&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, page2.StatusCode);
        var body2 = await page2.Content.ReadFromJsonAsync<PagedCategoriesResponse>();
        Assert.NotNull(body2);
        Assert.Equal(3, body2!.Total);
        Assert.Single(body2.Items);
        Assert.Equal(2, body2.Page);
    }

    [Fact]
    public async Task GetUserCategories_PaginationReturnsOnlyAuthenticatedUsersCategories()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "categories-page-scope-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "categories-page-scope-b@example.com");

        await CreateCategoryAsync(clientA, "alpha");
        await CreateCategoryAsync(clientA, "beta");
        await CreateCategoryAsync(clientB, "gamma");

        var response = await clientA.GetAsync("/api/categories?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedCategoriesResponse>();
        Assert.NotNull(paged);
        Assert.Equal(2, paged!.Total);
        Assert.Equal(2, paged.Items.Count);
        Assert.All(paged.Items, item => Assert.True(item.Name == "alpha" || item.Name == "beta"));
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
        HttpClient client,
        string url,
        string title,
        Guid categoryId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/links",
            new CreateLinkRequest(
                Url: url,
                Title: title,
                Description: null,
                TagIds: null,
                CategoryId: categoryId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
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
