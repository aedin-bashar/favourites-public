using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.User;

namespace Favourites.IntegrationTests.User;

public sealed class UserPreferencesEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task GetPreferences_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/user/preferences");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPreferences_WithNoStoredPreferences_ReturnsDefaults()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "preferences-defaults@example.com");

        var response = await client.GetAsync("/api/user/preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        Assert.NotNull(body);
        Assert.Equal("light", body!.Theme);
        Assert.Equal("comfortable", body.Density);
        Assert.True(body.AutoExtractTitle);
        Assert.True(body.ShowFavicon);
        Assert.True(body.OpenInNewTab);
        Assert.True(body.ConfirmBeforeDelete);
        Assert.True(body.SuggestTagsAutomatically);
        Assert.True(body.ShowColorsOnTagChips);
        Assert.False(body.WeeklySummaryEmail);
        Assert.True(body.SecurityAlerts);
        Assert.False(body.ProductUpdates);
    }

    [Fact]
    public async Task PatchPreferences_PersistsForCurrentUser()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "preferences-persist@example.com");
        var category = await CreateCategoryAsync(client, "Reading");

        var request = DefaultPatchRequest() with
        {
            Theme = "dark",
            Density = "compact",
            DefaultCategoryId = category.Id,
            AutoExtractTitle = false,
            WeeklySummaryEmail = true,
            ProductUpdates = true
        };

        var patchResponse = await client.PatchAsJsonAsync("/api/user/preferences", request);

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/user/preferences");
        var body = await getResponse.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        Assert.NotNull(body);
        Assert.Equal("dark", body!.Theme);
        Assert.Equal("compact", body.Density);
        Assert.Equal(category.Id, body.DefaultCategoryId);
        Assert.False(body.AutoExtractTitle);
        Assert.True(body.WeeklySummaryEmail);
        Assert.True(body.ProductUpdates);
    }

    [Fact]
    public async Task PatchPreferences_IsUserScoped_DoesNotAffectOtherUsers()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "preferences-scope-a@example.com");

        var patchResponse = await clientA.PatchAsJsonAsync(
            "/api/user/preferences",
            DefaultPatchRequest() with { Theme = "dark", SecurityAlerts = false });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "preferences-scope-b@example.com");

        var getResponse = await clientB.GetAsync("/api/user/preferences");
        var body = await getResponse.Content.ReadFromJsonAsync<UserPreferencesResponse>();

        Assert.NotNull(body);
        Assert.Equal("light", body!.Theme);
        Assert.True(body.SecurityAlerts);
    }

    [Fact]
    public async Task PatchPreferences_WithAnotherUsersDefaultCategory_ReturnsBadRequest()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "preferences-category-owner@example.com");
        var category = await CreateCategoryAsync(clientA, "Owner Category");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "preferences-category-other@example.com");

        var response = await clientB.PatchAsJsonAsync(
            "/api/user/preferences",
            DefaultPatchRequest() with { DefaultCategoryId = category.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
}
