using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;

namespace Favourites.IntegrationTests.Links;

public sealed class GetFavouriteLinkByIdEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task GetById_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/links/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenIdIsUnknown_ReturnsNotFound()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "by-id-unknown@example.com");

        var response = await client.GetAsync($"/api/links/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenLinkBelongsToCaller_ReturnsLink()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "by-id-owner@example.com");

        var created = await CreateLinkAsync(
            client,
            "https://example.com/owned",
            "Owned",
            "Owner can read.");

        var response = await client.GetAsync($"/api/links/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();

        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal(created.Url, body.Url);
        Assert.Equal(created.Title, body.Title);
        Assert.Equal(created.Description, body.Description);
        Assert.Equal(created.IsArchived, body.IsArchived);
        Assert.Equal(created.CreatedAtUtc, body.CreatedAtUtc);
        Assert.Equal(created.UpdatedAtUtc, body.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetById_WhenLinkBelongsToAnotherUser_ReturnsNotFound()
    {
        // Ownership-not-found rule (architecture invariant): a link owned by
        // another user must look identical to a missing id, otherwise an
        // attacker can enumerate other users' link ids by status code.
        // Expected response: 404, never 403.
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "by-id-cross-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "by-id-cross-b@example.com");

        var bLink = await CreateLinkAsync(
            clientB,
            "https://example.com/b-private",
            "B Private",
            null);

        var response = await clientA.GetAsync($"/api/links/{bLink.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<LinkResponse> CreateLinkAsync(
        HttpClient client,
        string url,
        string title,
        string? description)
    {
        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest(url, title, description));

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
