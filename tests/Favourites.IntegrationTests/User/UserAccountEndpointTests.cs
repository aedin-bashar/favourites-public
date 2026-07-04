using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;
using Favourites.Api.Contracts.User;
using Favourites.Domain.Entities;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.User;

public sealed class UserAccountEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task DeleteAccount_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/user/account");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RemovesCurrentUserAndOwnedData()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var userId = await RegisterAndLoginAsync(client, "delete-account@example.com");
        var category = await CreateCategoryAsync(client, "Delete Category");
        var tag = await CreateTagAsync(client, "delete-tag");
        await CreateLinkAsync(client, "https://example.com/delete-account", "Delete Account", category.Id, [tag.Id]);
        await client.PatchAsJsonAsync(
            "/api/user/preferences",
            DefaultPatchRequest() with { DefaultCategoryId = category.Id });
        await AddPasswordResetTokenAsync(factory, userId);

        var response = await client.DeleteAsync("/api/user/account");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var currentUserResponse = await client.GetAsync("/api/auth/current-user");
        Assert.Equal(HttpStatusCode.Unauthorized, currentUserResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("delete-account@example.com", ValidPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        Assert.False(await db.Users.AnyAsync(user => user.Id == userId));
        Assert.False(await db.FavouriteLinks.AnyAsync(link => link.UserId == userId));
        Assert.False(await db.Tags.AnyAsync(tagEntity => tagEntity.UserId == userId));
        Assert.False(await db.Categories.AnyAsync(categoryEntity => categoryEntity.UserId == userId));
        Assert.False(await db.UserPreferences.AnyAsync(preferences => preferences.UserId == userId));
        Assert.False(await db.PasswordResetTokens.AnyAsync(token => token.UserId == userId));
    }

    [Fact]
    public async Task DeleteAccount_DoesNotRemoveOtherUsersData()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "delete-account-keep@example.com");
        var keepLink = await CreateLinkAsync(
            clientA,
            "https://example.com/keep",
            "Keep Link",
            null,
            []);

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "delete-account-remove@example.com");
        await CreateLinkAsync(clientB, "https://example.com/remove", "Remove Link", null, []);

        var response = await clientB.DeleteAsync("/api/user/account");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var listResponse = await clientA.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var body = await listResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Items, link => link.Id == keepLink.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        Assert.False(await db.Users.AnyAsync(user => user.Id == userBId));
    }

    private static PatchUserPreferencesRequest DefaultPatchRequest() =>
        new(
            Theme: "light",
            Density: "comfortable",
            DefaultCategoryId: null,
            AutoExtractTitle: true,
            ShowFavicon: true,
            OpenInNewTab: true,
            ConfirmBeforeDelete: true,
            SuggestTagsAutomatically: true,
            ShowColorsOnTagChips: true,
            TagsDefaultSort: "name",
            CategoriesDefaultSort: "name",
            WeeklySummaryEmail: false,
            SecurityAlerts: true,
            ProductUpdates: false);

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

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
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

    private static async Task<LinkResponse> CreateLinkAsync(
        HttpClient client,
        string url,
        string title,
        Guid? categoryId,
        IReadOnlyCollection<Guid> tagIds)
    {
        var response = await client.PostAsJsonAsync(
            "/api/links",
            new CreateLinkRequest(url, title, null, tagIds, categoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(body);
        return body!;
    }

    private static async Task AddPasswordResetTokenAsync(FavouritesApiFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = $"hash-{Guid.NewGuid():N}",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();
    }
}
