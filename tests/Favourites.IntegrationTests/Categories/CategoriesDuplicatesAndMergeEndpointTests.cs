using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Links;
using Favourites.Domain.Entities;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Categories;

public sealed class CategoriesDuplicatesAndMergeEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    // ── GET /api/categories/duplicates ────────────────────────────────────────

    [Fact]
    public async Task GetDuplicates_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/categories/duplicates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDuplicates_NoCategories_ReturnsEmptyList()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "cat-dup-empty@example.com");

        var response = await client.GetAsync("/api/categories/duplicates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<CategoryDuplicateGroupResponse>>();
        Assert.NotNull(groups);
        Assert.Empty(groups!);
    }

    [Fact]
    public async Task GetDuplicates_CaseInsensitiveMatch_GroupsTogether()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        var userId = await RegisterAndLoginAsync(client, "cat-dup-case@example.com");

        // Bypass the unique-name validator and seed two case-variant categories directly.
        await SeedCategoriesDirectlyAsync(factory, userId, "Work", "work");

        var response = await client.GetAsync("/api/categories/duplicates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<CategoryDuplicateGroupResponse>>();
        Assert.NotNull(groups);
        Assert.Single(groups!);
        Assert.Equal(2, groups![0].Categories.Count);
    }

    [Fact]
    public async Task GetDuplicates_OnlyReturnsCurrentUsersGroups()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "cat-dup-iso-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "cat-dup-iso-b@example.com");

        // User B has a duplicate pair (seeded directly); user A has distinct categories.
        await SeedCategoriesDirectlyAsync(factory, userBId, "Tech", "tech");
        await CreateCategoryAsync(clientA, "Reading");
        await CreateCategoryAsync(clientA, "Cooking");

        var response = await clientA.GetAsync("/api/categories/duplicates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<CategoryDuplicateGroupResponse>>();
        Assert.NotNull(groups);
        Assert.Empty(groups!);
    }

    // ── POST /api/categories/merge ────────────────────────────────────────────

    [Fact]
    public async Task MergeCategories_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/categories/merge",
            new MergeCategoriesRequest(Guid.NewGuid(), new[] { Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MergeCategories_ValidRequest_DeletesMergedCategoriesAndReturnsCount()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        var userId = await RegisterAndLoginAsync(client, "cat-merge-basic@example.com");

        // "keep" created via API; the two merge categories seeded directly to bypass
        // unique-name validation for case-variant duplicates.
        var keep = await CreateCategoryAsync(client, "Work");
        var (merge1Id, merge2Id) = await SeedTwoCategoriesDirectlyAsync(factory, userId, "work", "WORK");

        var response = await client.PostAsJsonAsync("/api/categories/merge",
            new MergeCategoriesRequest(keep.Id, new[] { merge1Id, merge2Id }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var remaining = await db.Categories.AsNoTracking().ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(keep.Id, remaining[0].Id);
    }

    [Fact]
    public async Task MergeCategories_ReassignsLinksFromMergedCategoryToKeepCategory()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "cat-merge-links@example.com");

        var keep = await CreateCategoryAsync(client, "Keep");
        var merge = await CreateCategoryAsync(client, "Old");

        // Create a link assigned to the merge category.
        var linkResp = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com/cat-link", "Cat Link", null,
                CategoryId: merge.Id));
        Assert.Equal(HttpStatusCode.Created, linkResp.StatusCode);
        var link = await linkResp.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(link);

        var mergeResp = await client.PostAsJsonAsync("/api/categories/merge",
            new MergeCategoriesRequest(keep.Id, new[] { merge.Id }));
        Assert.Equal(HttpStatusCode.OK, mergeResp.StatusCode);

        // The link should now carry the keep category.
        var getResp = await client.GetAsync($"/api/links/{link!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var updated = await getResp.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(updated);
        Assert.NotNull(updated!.Category);
        Assert.Equal(keep.Id, updated.Category!.Id);
    }

    [Fact]
    public async Task MergeCategories_KeepCategoryBelongsToOtherUser_ReturnsNotFound()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "cat-merge-cross-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "cat-merge-cross-b@example.com");

        var bKeep = await CreateCategoryAsync(clientB, "B-Keep");
        var aCat = await CreateCategoryAsync(clientA, "A-Cat");

        var response = await clientA.PostAsJsonAsync("/api/categories/merge",
            new MergeCategoriesRequest(bKeep.Id, new[] { aCat.Id }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MergeCategories_MergeCategoriesBelongToOtherUser_MergesZeroCategories()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "cat-merge-other-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "cat-merge-other-b@example.com");

        var aKeep = await CreateCategoryAsync(clientA, "A-Keep");
        var bCat = await CreateCategoryAsync(clientB, "B-Cat");

        var response = await clientA.PostAsJsonAsync("/api/categories/merge",
            new MergeCategoriesRequest(aKeep.Id, new[] { bCat.Id }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // B's category must still exist.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        var bCatStored = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == bCat.Id);
        Assert.NotNull(bCatStored);
    }

    [Fact]
    public async Task MergeCategories_EmptyMergeList_ReturnsBadRequest()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "cat-merge-empty@example.com");

        var keep = await CreateCategoryAsync(client, "Keep");

        var response = await client.PostAsJsonAsync("/api/categories/merge",
            new MergeCategoriesRequest(keep.Id, Array.Empty<Guid>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(body);
        return body!;
    }

    private static async Task SeedCategoriesDirectlyAsync(
        FavouritesApiFactory factory, Guid userId, params string[] names)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        foreach (var name in names)
            db.Categories.Add(Category.Create(userId, name));
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid, Guid)> SeedTwoCategoriesDirectlyAsync(
        FavouritesApiFactory factory, Guid userId, string name1, string name2)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        var c1 = Category.Create(userId, name1);
        var c2 = Category.Create(userId, name2);
        db.Categories.Add(c1);
        db.Categories.Add(c2);
        await db.SaveChangesAsync();
        return (c1.Id, c2.Id);
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
