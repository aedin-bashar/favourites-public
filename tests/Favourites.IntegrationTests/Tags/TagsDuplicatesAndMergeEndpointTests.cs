using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;
using Favourites.Domain.Entities;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Tags;

public sealed class TagsDuplicatesAndMergeEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    // ── GET /api/tags/duplicates ──────────────────────────────────────────────

    [Fact]
    public async Task GetDuplicates_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/duplicates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDuplicates_NoTagsAtAll_ReturnsEmptyList()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "dup-empty@example.com");

        var response = await client.GetAsync("/api/tags/duplicates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<TagDuplicateGroupResponse>>();
        Assert.NotNull(groups);
        Assert.Empty(groups!);
    }

    [Fact]
    public async Task GetDuplicates_CaseInsensitiveMatch_GroupsTogether()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        var userId = await RegisterAndLoginAsync(client, "dup-case@example.com");

        // Bypass the unique-name validator and seed two case-variant tags directly
        // to test the duplicate-detection logic specifically.
        await SeedTagsDirectlyAsync(factory, userId, "dotnet", "DotNet");

        var response = await client.GetAsync("/api/tags/duplicates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<TagDuplicateGroupResponse>>();
        Assert.NotNull(groups);
        Assert.Single(groups!);
        Assert.Equal(2, groups![0].Tags.Count);
    }

    [Fact]
    public async Task GetDuplicates_LevenshteinDistance2_GroupsTogether()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        var userId = await RegisterAndLoginAsync(client, "dup-levenshtein@example.com");

        // "dotnet" vs "d0tne" — distance 2 (two char substitutions).
        // Seeded directly to bypass the unique-name validator.
        await SeedTagsDirectlyAsync(factory, userId, "dotnet", "d0tne");

        var response = await client.GetAsync("/api/tags/duplicates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<TagDuplicateGroupResponse>>();
        Assert.NotNull(groups);
        Assert.Single(groups!);
        Assert.Equal(2, groups![0].Tags.Count);
    }

    [Fact]
    public async Task GetDuplicates_LevenshteinDistanceOver2_NotGrouped()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "dup-far@example.com");

        // "abc" vs "xyz" — distance 3, should NOT be grouped.
        await CreateTagAsync(client, "abc");
        await CreateTagAsync(client, "xyz");

        var response = await client.GetAsync("/api/tags/duplicates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<TagDuplicateGroupResponse>>();
        Assert.NotNull(groups);
        Assert.Empty(groups!);
    }

    [Fact]
    public async Task GetDuplicates_OnlyReturnsCurrentUsersGroups()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        var userAId = await RegisterAndLoginAsync(clientA, "dup-iso-a@example.com");

        using var clientB = factory.CreateClient();
        var userBId = await RegisterAndLoginAsync(clientB, "dup-iso-b@example.com");

        // User B has a duplicate pair (seeded directly); user A has distinct tags.
        await SeedTagsDirectlyAsync(factory, userBId, "reading", "Reading");
        await CreateTagAsync(clientA, "alpha");
        await CreateTagAsync(clientA, "beta");

        var response = await clientA.GetAsync("/api/tags/duplicates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<List<TagDuplicateGroupResponse>>();
        Assert.NotNull(groups);
        Assert.Empty(groups!);
    }

    // ── POST /api/tags/merge ──────────────────────────────────────────────────

    [Fact]
    public async Task MergeTags_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tags/merge",
            new MergeTagsRequest(Guid.NewGuid(), new[] { Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MergeTags_ValidRequest_DeletesMergedTagsAndReturnsCount()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        var userId = await RegisterAndLoginAsync(client, "merge-basic@example.com");

        // "keep" created via API; the two merge tags seeded directly to bypass
        // unique-name validation for case-variant duplicates.
        var keep = await CreateTagAsync(client, "dotnet");
        var (merge1Id, merge2Id) = await SeedTwoTagsDirectlyAsync(factory, userId, "DotNet", ".NET");

        var response = await client.PostAsJsonAsync("/api/tags/merge",
            new MergeTagsRequest(keep.Id, new[] { merge1Id, merge2Id }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var remaining = await db.Tags.AsNoTracking().ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(keep.Id, remaining[0].Id);
    }

    [Fact]
    public async Task MergeTags_ReassignsLinksFromMergedTagToKeepTag()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "merge-links@example.com");

        var keep = await CreateTagAsync(client, "keep");
        var mergeTag = await CreateTagAsync(client, "old");

        // Create a link tagged with the merge tag.
        var linkResp = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com/link1", "Link 1", null, new[] { mergeTag.Id }));
        Assert.Equal(HttpStatusCode.Created, linkResp.StatusCode);
        var link = await linkResp.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(link);

        var mergeResp = await client.PostAsJsonAsync("/api/tags/merge",
            new MergeTagsRequest(keep.Id, new[] { mergeTag.Id }));
        Assert.Equal(HttpStatusCode.OK, mergeResp.StatusCode);

        // The link should now carry the keep tag.
        var getResp = await client.GetAsync($"/api/links/{link!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var updated = await getResp.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(updated);
        Assert.Single(updated!.Tags);
        Assert.Equal(keep.Id, updated.Tags[0].Id);
    }

    [Fact]
    public async Task MergeTags_LinkAlreadyTaggedWithKeep_DeduplicatesRatherThanCreatingDuplicate()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "merge-dedup@example.com");

        var keep = await CreateTagAsync(client, "keep");
        var mergeTag = await CreateTagAsync(client, "old");

        // Link already has both tags.
        var linkResp = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com/both-tags", "Both", null,
                new[] { keep.Id, mergeTag.Id }));
        Assert.Equal(HttpStatusCode.Created, linkResp.StatusCode);
        var link = await linkResp.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(link);

        var mergeResp = await client.PostAsJsonAsync("/api/tags/merge",
            new MergeTagsRequest(keep.Id, new[] { mergeTag.Id }));
        Assert.Equal(HttpStatusCode.OK, mergeResp.StatusCode);

        // The link must still have exactly one tag (no duplicates).
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var tagCount = await db.FavouriteLinkTags.AsNoTracking()
            .CountAsync(lt => lt.FavouriteLinkId == link!.Id);

        Assert.Equal(1, tagCount);
    }

    [Fact]
    public async Task MergeTags_KeepTagBelongsToOtherUser_ReturnsNotFound()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "merge-cross-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "merge-cross-b@example.com");

        var bKeep = await CreateTagAsync(clientB, "b-keep");
        var aTag = await CreateTagAsync(clientA, "a-tag");

        // User A tries to keep a tag that belongs to user B.
        var response = await clientA.PostAsJsonAsync("/api/tags/merge",
            new MergeTagsRequest(bKeep.Id, new[] { aTag.Id }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MergeTags_MergeTagsBelongToOtherUser_MergesZeroTags()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "merge-other-merge-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "merge-other-merge-b@example.com");

        var aKeep = await CreateTagAsync(clientA, "a-keep");
        var bTag = await CreateTagAsync(clientB, "b-tag");

        // User A tries to merge a tag that belongs to B into A's keep.
        var response = await clientA.PostAsJsonAsync("/api/tags/merge",
            new MergeTagsRequest(aKeep.Id, new[] { bTag.Id }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // B's tag must still exist.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        var bTagStored = await db.Tags.AsNoTracking().SingleOrDefaultAsync(t => t.Id == bTag.Id);
        Assert.NotNull(bTagStored);
    }

    [Fact]
    public async Task MergeTags_EmptyMergeList_ReturnsBadRequest()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "merge-empty-list@example.com");

        var keep = await CreateTagAsync(client, "keep");

        var response = await client.PostAsJsonAsync("/api/tags/merge",
            new MergeTagsRequest(keep.Id, Array.Empty<Guid>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<TagResponse> CreateTagAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(body);
        return body!;
    }

    private static async Task SeedTagsDirectlyAsync(
        FavouritesApiFactory factory, Guid userId, params string[] names)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        foreach (var name in names)
            db.Tags.Add(Tag.Create(userId, name));
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid, Guid)> SeedTwoTagsDirectlyAsync(
        FavouritesApiFactory factory, Guid userId, string name1, string name2)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        var t1 = Tag.Create(userId, name1);
        var t2 = Tag.Create(userId, name2);
        db.Tags.Add(t1);
        db.Tags.Add(t2);
        await db.SaveChangesAsync();
        return (t1.Id, t2.Id);
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
