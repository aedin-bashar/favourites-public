using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Categories;

public sealed class CategoryDuplicateNameEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task CreateCategory_WithDuplicateNameForSameUser_ReturnsValidationError()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "categories-duplicate-create@example.com");
        var created = await CreateCategoryAsync(client, "reading");

        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("  READING  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateCategoryRequest.Name), problem!.Errors.Keys);

        var listResponse = await client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var categories = await listResponse.Content.ReadFromJsonAsync<PagedCategoriesResponse>();
        Assert.NotNull(categories);
        Assert.Single(categories!.Items);
        Assert.Equal(created.Id, categories.Items[0].Id);
    }

    [Fact]
    public async Task CreateCategory_AllowsSameNameForDifferentUsers()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "categories-duplicate-create-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "categories-duplicate-create-b@example.com");

        var aCategory = await CreateCategoryAsync(clientA, "reading");
        var bCategory = await CreateCategoryAsync(clientB, "reading");

        Assert.NotEqual(aCategory.Id, bCategory.Id);
        Assert.Equal(aCategory.Name, bCategory.Name);
    }

    [Fact]
    public async Task UpdateCategory_WithDuplicateNameForSameUser_ReturnsValidationErrorAndLeavesCategoryUnchanged()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "categories-duplicate-update@example.com");
        await CreateCategoryAsync(client, "reading");
        var dotnet = await CreateCategoryAsync(client, "dotnet");

        var response = await client.PutAsJsonAsync(
            $"/api/categories/{dotnet.Id}",
            new UpdateCategoryRequest("  READING  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(UpdateCategoryRequest.Name), problem!.Errors.Keys);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var stored = await dbContext.Categories.AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == dotnet.Id);

        Assert.NotNull(stored);
        Assert.Equal("dotnet", stored!.Name);
    }

    [Fact]
    public async Task UpdateCategory_AllowsKeepingSameNameForSameCategory()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "categories-duplicate-update-same@example.com");
        var created = await CreateCategoryAsync(client, "reading");

        var response = await client.PutAsJsonAsync(
            $"/api/categories/{created.Id}",
            new UpdateCategoryRequest("  reading  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("reading", body.Name);
    }

    [Fact]
    public async Task UpdateCategory_AllowsNameUsedByAnotherUser()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "categories-duplicate-update-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "categories-duplicate-update-b@example.com");

        await CreateCategoryAsync(clientA, "reading");
        var bCategory = await CreateCategoryAsync(clientB, "dotnet");

        var response = await clientB.PutAsJsonAsync(
            $"/api/categories/{bCategory.Id}",
            new UpdateCategoryRequest("reading"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(body);
        Assert.Equal(bCategory.Id, body!.Id);
        Assert.Equal("reading", body.Name);
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
