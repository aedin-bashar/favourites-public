using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Links;

public sealed class ImportLinksEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    private const string SimpleBookmarksHtml = """
        <!DOCTYPE NETSCAPE-Bookmark-file-1>
        <META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">
        <TITLE>Bookmarks</TITLE>
        <H1>Bookmarks</H1>
        <DL><p>
            <DT><H3>Tech</H3>
            <DL><p>
                <DT><A HREF="https://angular.dev" ADD_DATE="1700000000">Angular</A>
                <DT><A HREF="https://dotnet.microsoft.com" ADD_DATE="1700000001">.NET</A>
            </DL><p>
            <DT><A HREF="https://example.com/no-folder">No folder link</A>
        </DL><p>
        """;

    [Fact]
    public async Task Import_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        using var content = MakeHtmlContent("<html></html>", "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Import_ValidBookmarksFile_CreatesLinksAndReturnsCreatedCount()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-basic@example.com");

        using var content = MakeHtmlContent(SimpleBookmarksHtml, "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ImportLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Created);
        Assert.Equal(0, body.Skipped);
    }

    [Fact]
    public async Task Import_ValidBookmarksFile_PersistsLinksInDatabase()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-persist@example.com");

        using var content = MakeHtmlContent(SimpleBookmarksHtml, "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var urls = await db.FavouriteLinks.AsNoTracking()
            .Select(l => l.Url).ToListAsync();

        // The URL normalizer appends a trailing slash to bare domain URLs.
        Assert.Contains("https://angular.dev/", urls);
        Assert.Contains("https://dotnet.microsoft.com/", urls);
        Assert.Contains("https://example.com/no-folder", urls);
    }

    [Fact]
    public async Task Import_DuplicateUrlInSameFile_SkipsDuplicate()
    {
        const string html = """
            <!DOCTYPE NETSCAPE-Bookmark-file-1>
            <DL><p>
                <DT><A HREF="https://example.com/dup">First</A>
                <DT><A HREF="https://example.com/dup">Duplicate</A>
                <DT><A HREF="https://example.com/unique">Unique</A>
            </DL><p>
            """;

        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-dup-file@example.com");

        using var content = MakeHtmlContent(html, "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ImportLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Created);
        Assert.Equal(1, body.Skipped);
    }

    [Fact]
    public async Task Import_UrlAlreadyInLibrary_SkipsExistingUrl()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-existing@example.com");

        // Pre-create a link with the normalised URL (domain adds trailing slash).
        var createResp = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://angular.dev/", "Angular", null));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        using var content = MakeHtmlContent(SimpleBookmarksHtml, "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ImportLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Created);
        Assert.Equal(1, body.Skipped);
    }

    [Fact]
    public async Task Import_FolderNamesBecomeTags_ScopedToCurrentUser()
    {
        // Real browser exports (Chrome/Firefox/Safari/Edge) nest a folder's
        // links in a <DL> after the folder's <DT><H3>, and leave <DT>/<p>
        // unclosed. Links outside a folder's <DL> must not inherit its tag.
        const string html = """
            <!DOCTYPE NETSCAPE-Bookmark-file-1>
            <DL><p>
                <DT><H3>Coding</H3>
                <DL><p>
                    <DT><A HREF="https://github.com/">GitHub</A>
                    <DT><H3>Nested</H3>
                    <DL><p>
                        <DT><A HREF="https://deep.example.com/">Deep</A>
                    </DL><p>
                </DL><p>
                <DT><A HREF="https://rootlevel.example.com/">Root level</A>
            </DL><p>
            """;

        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-tags@example.com");

        using var content = MakeHtmlContent(html, "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ImportLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Created);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var tagNames = await db.Tags.AsNoTracking().Select(t => t.Name).ToListAsync();
        Assert.Contains("Coding", tagNames);
        Assert.Contains("Nested", tagNames);

        var linkTags = await (
            from lt in db.FavouriteLinkTags.AsNoTracking()
            join l in db.FavouriteLinks.AsNoTracking() on lt.FavouriteLinkId equals l.Id
            join t in db.Tags.AsNoTracking() on lt.TagId equals t.Id
            select new { l.Url, t.Name }).ToListAsync();

        Assert.Contains(linkTags, x => x.Url == "https://github.com/" && x.Name == "Coding");
        // Innermost folder wins for nested links.
        Assert.Contains(linkTags, x => x.Url == "https://deep.example.com/" && x.Name == "Nested");
        // Root-level links that merely follow a folder get no tag.
        Assert.DoesNotContain(linkTags, x => x.Url == "https://rootlevel.example.com/");
    }

    [Fact]
    public async Task Import_ExistingTagWithSameName_ReusesTagRatherThanCreatingDuplicate()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-reuse-tag@example.com");

        // Pre-create a tag named "Tech".
        var tagResp = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Tech"));
        Assert.Equal(HttpStatusCode.Created, tagResp.StatusCode);
        var existingTag = await tagResp.Content.ReadFromJsonAsync<TagResponse>();
        Assert.NotNull(existingTag);

        using var content = MakeHtmlContent(SimpleBookmarksHtml, "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var techTagCount = await db.Tags.AsNoTracking()
            .CountAsync(t => t.Name == "Tech");

        Assert.Equal(1, techTagCount);
    }

    [Fact]
    public async Task Import_ImportedLinks_AreNotVisibleToOtherUser()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "import-isolation-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "import-isolation-b@example.com");

        using var content = MakeHtmlContent(SimpleBookmarksHtml, "bookmarks.html");
        var importResp = await clientA.PostAsync("/api/links/import", content);
        Assert.Equal(HttpStatusCode.OK, importResp.StatusCode);

        // User B should see zero links.
        var listResp = await clientB.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var paged = await listResp.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(paged);
        Assert.Equal(0, paged!.Total);
    }

    [Fact]
    public async Task Import_BrowserFileWithTagsAttribute_CreatesTagsButNoCategories()
    {
        // No FAVOURITES-EXPORT marker → browser semantics: folders become tags,
        // TAGS attributes (old Firefox) also become tags, nothing becomes a category.
        const string html = """
            <!DOCTYPE NETSCAPE-Bookmark-file-1>
            <DL><p>
                <DT><H3>Coding</H3>
                <DL><p>
                    <DT><A HREF="https://github.com/" TAGS="git,oss">GitHub</A>
                </DL><p>
            </DL><p>
            """;

        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-tags-attr@example.com");

        using var content = MakeHtmlContent(html, "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var tagNames = await db.Tags.AsNoTracking().Select(t => t.Name).ToListAsync();
        Assert.Contains("Coding", tagNames);
        Assert.Contains("git", tagNames);
        Assert.Contains("oss", tagNames);

        Assert.Equal(0, await db.Categories.AsNoTracking().CountAsync());

        var link = await db.FavouriteLinks.AsNoTracking().SingleAsync();
        Assert.Null(link.CategoryId);
        Assert.Equal(3, await db.FavouriteLinkTags.AsNoTracking()
            .CountAsync(lt => lt.FavouriteLinkId == link.Id));
    }

    [Fact]
    public async Task Import_NonHtmlFile_ReturnsBadRequest()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-non-html@example.com");

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("not html content"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "bookmarks.txt");

        var response = await client.PostAsync("/api/links/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_EmptyFile_ReturnsBadRequest()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-empty@example.com");

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Array.Empty<byte>());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        form.Add(fileContent, "file", "bookmarks.html");

        var response = await client.PostAsync("/api/links/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_HtmlWithNoAnchors_ReturnsZeroCreated()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-no-links@example.com");

        const string html = "<!DOCTYPE html><html><body><p>No links here</p></body></html>";
        using var content = MakeHtmlContent(html, "bookmarks.html");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ImportLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.Created);
        Assert.Equal(0, body.Skipped);
    }

    private static MultipartFormDataContent MakeHtmlContent(string html, string fileName)
    {
        var form = new MultipartFormDataContent();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(html);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        form.Add(fileContent, "file", fileName);
        return form;
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
