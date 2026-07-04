using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Links;

public sealed class ExportLinksEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task Export_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/links/export");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_Json_ReturnsJsonContentType()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "export-json-ct@example.com");

        await CreateLinkAsync(client, "https://example.com/e1", "Export 1");

        var response = await client.GetAsync("/api/links/export?format=json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Export_Json_ContainsAllActiveAndArchivedLinks()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "export-json-all@example.com");

        var active = await CreateLinkAsync(client, "https://example.com/active", "Active");
        var archived = await CreateLinkAsync(client, "https://example.com/archived", "Archived");

        // Archive the second link.
        var archiveResp = await client.PostAsync($"/api/links/{archived.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveResp.StatusCode);

        var response = await client.GetAsync("/api/links/export?format=json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("https://example.com/active", json);
        Assert.Contains("https://example.com/archived", json);
    }

    [Fact]
    public async Task Export_Json_DefaultFormatIsJson()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "export-default@example.com");

        await CreateLinkAsync(client, "https://example.com/default", "Default");

        // No ?format query param — should default to JSON.
        var response = await client.GetAsync("/api/links/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Export_Html_ReturnsHtmlContentType()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "export-html-ct@example.com");

        await CreateLinkAsync(client, "https://example.com/html1", "HTML Link");

        var response = await client.GetAsync("/api/links/export?format=html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Export_Html_IsNetscapeBookmarkFile()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "export-html-doctype@example.com");

        await CreateLinkAsync(client, "https://example.com/html-doctype", "Doctype Test");

        var response = await client.GetAsync("/api/links/export?format=html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<!DOCTYPE NETSCAPE-Bookmark-file-1>", html);
        Assert.Contains("<H1>Bookmarks</H1>", html);
    }

    [Fact]
    public async Task Export_Html_ContainsLinkAsAnchorWithAddDate()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "export-html-rows@example.com");

        await CreateLinkAsync(client, "https://example.com/html-data", "HTML Data");

        var response = await client.GetAsync("/api/links/export?format=html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<DT><A HREF=\"https://example.com/html-data\"", html);
        Assert.Contains("ADD_DATE=", html);
        Assert.Contains(">HTML Data</A>", html);
    }

    [Fact]
    public async Task Export_Html_ContainsFavouritesMarkerAndTagsAttribute()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "export-html-marker@example.com");

        var tagResp = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Tech"));
        Assert.Equal(HttpStatusCode.Created, tagResp.StatusCode);
        var tag = await tagResp.Content.ReadFromJsonAsync<TagResponse>();

        var linkResp = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com/tagged", "Tagged", null, [tag!.Id]));
        Assert.Equal(HttpStatusCode.Created, linkResp.StatusCode);

        var response = await client.GetAsync("/api/links/export?format=html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!-- FAVOURITES-EXPORT -->", html);
        Assert.Contains("TAGS=\"Tech\"", html);
    }

    [Fact]
    public async Task Export_Html_RoundTripsTagsAndCategoriesThroughHtmlImport()
    {
        await using var factory = new FavouritesApiFactory();

        using var source = factory.CreateClient();
        await RegisterAndLoginAsync(source, "export-html-rt-src@example.com");

        var tagResp = await source.PostAsJsonAsync("/api/tags", new CreateTagRequest("Tech"));
        Assert.Equal(HttpStatusCode.Created, tagResp.StatusCode);
        var tag = await tagResp.Content.ReadFromJsonAsync<TagResponse>();

        var catResp = await source.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Docs"));
        Assert.Equal(HttpStatusCode.Created, catResp.StatusCode);
        var category = await catResp.Content.ReadFromJsonAsync<CategoryResponse>();

        var linkResp = await source.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://example.com/rt-1", "Round Trip 1", null, [tag!.Id], category!.Id));
        Assert.Equal(HttpStatusCode.Created, linkResp.StatusCode);
        await CreateLinkAsync(source, "https://example.com/rt-2", "Round Trip 2");

        var export = await source.GetAsync("/api/links/export?format=html");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var html = await export.Content.ReadAsStringAsync();

        using var target = factory.CreateClient();
        var targetUserId = await RegisterAndLoginAsync(target, "export-html-rt-dst@example.com");

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(html));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
        form.Add(fileContent, "file", "favourites-bookmarks.html");

        var import = await target.PostAsync("/api/links/import", form);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);

        var body = await import.Content.ReadAsStringAsync();
        Assert.Contains("\"created\":2", body);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        // Category restored as a category (not converted into a tag).
        var docsCategory = await db.Categories.AsNoTracking()
            .SingleAsync(c => c.UserId == targetUserId && c.Name == "Docs");
        var restoredLink = await db.FavouriteLinks.AsNoTracking()
            .SingleAsync(l => l.UserId == targetUserId && l.Url == "https://example.com/rt-1");
        Assert.Equal(docsCategory.Id, restoredLink.CategoryId);

        var targetTagNames = await db.Tags.AsNoTracking()
            .Where(t => t.UserId == targetUserId)
            .Select(t => t.Name)
            .ToListAsync();
        Assert.Contains("Tech", targetTagNames);
        Assert.DoesNotContain("Docs", targetTagNames);

        // The tag is linked to the restored link.
        var restoredLinkTagCount = await db.FavouriteLinkTags.AsNoTracking()
            .CountAsync(lt => lt.FavouriteLinkId == restoredLink.Id);
        Assert.Equal(1, restoredLinkTagCount);
    }

    [Fact]
    public async Task Export_OnlyReturnsCurrentUsersLinks()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "export-iso-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "export-iso-b@example.com");

        await CreateLinkAsync(clientA, "https://example.com/user-a", "User A Link");
        await CreateLinkAsync(clientB, "https://example.com/user-b", "User B Link");

        var response = await clientA.GetAsync("/api/links/export?format=json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("https://example.com/user-a", json);
        Assert.DoesNotContain("https://example.com/user-b", json);
    }

    [Fact]
    public async Task Export_NoLinks_ReturnsBadRequest()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "export-empty@example.com");

        var jsonResponse = await client.GetAsync("/api/links/export?format=json");
        Assert.Equal(HttpStatusCode.BadRequest, jsonResponse.StatusCode);

        var htmlResponse = await client.GetAsync("/api/links/export?format=html");
        Assert.Equal(HttpStatusCode.BadRequest, htmlResponse.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<LinkResponse> CreateLinkAsync(HttpClient client, string url, string title)
    {
        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, null));
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
