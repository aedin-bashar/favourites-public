using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Links;

public sealed class ImportJsonLinksEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    // Mirrors the shape produced by GET /api/links/export?format=json.
    private const string SimpleExportJson = """
        [
          {
            "id": "0b6a4a1e-0000-0000-0000-000000000001",
            "url": "https://angular.dev/",
            "title": "Angular",
            "description": "Framework docs",
            "isArchived": false,
            "createdAtUtc": "2023-11-14T22:13:20+00:00",
            "updatedAtUtc": null,
            "tags": [ { "id": "0b6a4a1e-0000-0000-0000-00000000a001", "name": "Tech" } ],
            "category": { "id": "0b6a4a1e-0000-0000-0000-00000000c001", "name": "Docs", "color": "#0d9488" }
          },
          {
            "id": "0b6a4a1e-0000-0000-0000-000000000002",
            "url": "https://example.com/archived",
            "title": "Old link",
            "description": null,
            "isArchived": true,
            "createdAtUtc": "2022-01-01T00:00:00+00:00",
            "updatedAtUtc": null,
            "tags": [],
            "category": null
          }
        ]
        """;

    [Fact]
    public async Task ImportJson_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        using var content = MakeJsonContent("[]", "favourites-export.json");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ImportJson_ValidExport_CreatesLinksAndReturnsCreatedCount()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-json-basic@example.com");

        using var content = MakeJsonContent(SimpleExportJson, "favourites-export.json");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ImportLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Created);
        Assert.Equal(0, body.Skipped);
    }

    [Fact]
    public async Task ImportJson_RestoresTagsCategoriesDescriptionAndArchivedState()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-json-restore@example.com");

        using var content = MakeJsonContent(SimpleExportJson, "favourites-export.json");
        var response = await client.PostAsync("/api/links/import", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();

        var angular = await db.FavouriteLinks.AsNoTracking()
            .SingleAsync(l => l.Url == "https://angular.dev/");
        Assert.Equal("Framework docs", angular.Description);
        Assert.False(angular.IsArchived);
        Assert.Equal(DateTimeOffset.Parse("2023-11-14T22:13:20+00:00"), angular.CreatedAtUtc);

        var archived = await db.FavouriteLinks.AsNoTracking()
            .SingleAsync(l => l.Url == "https://example.com/archived");
        Assert.True(archived.IsArchived);

        var tagNames = await db.Tags.AsNoTracking().Select(t => t.Name).ToListAsync();
        Assert.Contains("Tech", tagNames);

        var categoryNames = await db.Categories.AsNoTracking().Select(c => c.Name).ToListAsync();
        Assert.Contains("Docs", categoryNames);

        var docsCategory = await db.Categories.AsNoTracking().SingleAsync(c => c.Name == "Docs");
        Assert.Equal(docsCategory.Id, angular.CategoryId);

        var linkTagCount = await db.FavouriteLinkTags.AsNoTracking()
            .CountAsync(lt => lt.FavouriteLinkId == angular.Id);
        Assert.Equal(1, linkTagCount);
    }

    [Fact]
    public async Task ImportJson_UrlAlreadyInLibrary_SkipsExistingUrl()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-json-dup@example.com");

        var createResp = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest("https://angular.dev/", "Angular", null));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        using var content = MakeJsonContent(SimpleExportJson, "favourites-export.json");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ImportLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.Created);
        Assert.Equal(1, body.Skipped);
    }

    [Fact]
    public async Task ImportJson_MalformedJson_ReturnsBadRequest()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "import-json-malformed@example.com");

        using var content = MakeJsonContent("{ not valid json ]", "favourites-export.json");
        var response = await client.PostAsync("/api/links/import", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportJson_RoundTripsThroughJsonExport()
    {
        await using var factory = new FavouritesApiFactory();

        using var source = factory.CreateClient();
        await RegisterAndLoginAsync(source, "import-json-rt-src@example.com");
        await CreateLinkAsync(source, "https://example.com/rt-json-1", "RT Json 1");
        await CreateLinkAsync(source, "https://example.com/rt-json-2", "RT Json 2");

        var export = await source.GetAsync("/api/links/export?format=json");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var json = await export.Content.ReadAsStringAsync();

        using var target = factory.CreateClient();
        await RegisterAndLoginAsync(target, "import-json-rt-dst@example.com");

        using var content = MakeJsonContent(json, "favourites-export.json");
        var import = await target.PostAsync("/api/links/import", content);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);

        var body = await import.Content.ReadFromJsonAsync<ImportLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Created);
        Assert.Equal(0, body.Skipped);
    }

    private static MultipartFormDataContent MakeJsonContent(string json, string fileName)
    {
        var form = new MultipartFormDataContent();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(json);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(fileContent, "file", fileName);
        return form;
    }

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
