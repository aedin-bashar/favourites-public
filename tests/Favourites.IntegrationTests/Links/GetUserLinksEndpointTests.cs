using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Links;

namespace Favourites.IntegrationTests.Links;

public sealed class GetUserLinksEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task GetUserLinks_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/links");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserLinks_WhenUserHasNoLinks_ReturnsEmptyPagedResult()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "no-links@example.com");

        var response = await client.GetAsync("/api/links");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();

        Assert.NotNull(body);
        Assert.Empty(body!.Items);
        Assert.Equal(0, body.Total);
    }

    [Fact]
    public async Task GetUserLinks_ReturnsOnlyAuthenticatedUsersLinks()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientA = factory.CreateClient();
        await RegisterAndLoginAsync(clientA, "list-owner-a@example.com");

        using var clientB = factory.CreateClient();
        await RegisterAndLoginAsync(clientB, "list-owner-b@example.com");

        var aFirst = await CreateLinkAsync(clientA, "https://example.com/a-first", "A First");
        var aSecond = await CreateLinkAsync(clientA, "https://example.com/a-second", "A Second");
        var bOnly = await CreateLinkAsync(clientB, "https://example.com/b-only", "B Only");

        var aResponse = await clientA.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, aResponse.StatusCode);
        var aBody = await aResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(aBody);
        Assert.Equal(2, aBody!.Items.Count);
        Assert.Equal(2, aBody.Total);

        var aIds = aBody.Items.Select(l => l.Id).ToHashSet();
        Assert.Contains(aFirst.Id, aIds);
        Assert.Contains(aSecond.Id, aIds);
        Assert.DoesNotContain(bOnly.Id, aIds);

        var bResponse = await clientB.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, bResponse.StatusCode);
        var bBody = await bResponse.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(bBody);
        Assert.Single(bBody!.Items);
        Assert.Equal(1, bBody.Total);
        Assert.Equal(bOnly.Id, bBody.Items[0].Id);
        Assert.DoesNotContain(aFirst.Id, bBody.Items.Select(l => l.Id));
        Assert.DoesNotContain(aSecond.Id, bBody.Items.Select(l => l.Id));
    }

    [Fact]
    public async Task GetUserLinks_ReturnsNewestFirst()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "ordering@example.com");

        var first = await CreateLinkAsync(client, "https://example.com/first", "First");
        await Task.Delay(20);
        var second = await CreateLinkAsync(client, "https://example.com/second", "Second");
        await Task.Delay(20);
        var third = await CreateLinkAsync(client, "https://example.com/third", "Third");

        var response = await client.GetAsync("/api/links");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Items.Count);
        Assert.Equal(third.Id, body.Items[0].Id);
        Assert.Equal(second.Id, body.Items[1].Id);
        Assert.Equal(first.Id, body.Items[2].Id);
    }

    [Fact]
    public async Task GetUserLinks_WithPagination_ReturnsCorrectPage()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        await RegisterAndLoginAsync(client, "pagination@example.com");

        // Create 5 links in order
        for (var i = 1; i <= 5; i++)
        {
            await CreateLinkAsync(client, $"https://example.com/link-{i}", $"Link {i}");
            await Task.Delay(10);
        }

        // Page 1 of 2 (pageSize=3)
        var page1 = await client.GetAsync("/api/links?page=1&pageSize=3");
        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        var body1 = await page1.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body1);
        Assert.Equal(5, body1!.Total);
        Assert.Equal(3, body1.Items.Count);
        Assert.Equal(1, body1.Page);
        Assert.Equal(3, body1.PageSize);

        // Page 2 of 2 — remaining 2 items
        var page2 = await client.GetAsync("/api/links?page=2&pageSize=3");
        Assert.Equal(HttpStatusCode.OK, page2.StatusCode);
        var body2 = await page2.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body2);
        Assert.Equal(5, body2!.Total);
        Assert.Equal(2, body2.Items.Count);
        Assert.Equal(2, body2.Page);
    }

    [Fact]
    public async Task GetUserLinks_PaginatedPage_OnlyContainsAuthenticatedUsersLinks()
    {
        await using var factory = new FavouritesApiFactory();

        using var clientOwner = factory.CreateClient();
        await RegisterAndLoginAsync(clientOwner, "paged-owner@example.com");

        using var clientOther = factory.CreateClient();
        await RegisterAndLoginAsync(clientOther, "paged-other@example.com");

        // Other user creates links that must not bleed into owner's results
        await CreateLinkAsync(clientOther, "https://other.com/1", "Other 1");
        await CreateLinkAsync(clientOther, "https://other.com/2", "Other 2");

        // Owner creates 3 links
        for (var i = 1; i <= 3; i++)
            await CreateLinkAsync(clientOwner, $"https://owner.com/{i}", $"Owner {i}");

        var response = await clientOwner.GetAsync("/api/links?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedLinksResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Total);
        Assert.Equal(3, body.Items.Count);
        Assert.All(body.Items, item => Assert.StartsWith("https://owner.com/", item.Url));
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
