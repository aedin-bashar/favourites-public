using System.Net;

namespace Favourites.IntegrationTests.Auth;

public sealed class ProtectedEndpointsTests
{
    [Theory]
    [InlineData("GET", "/api/auth/current-user")]
    [InlineData("POST", "/api/auth/logout")]
    public async Task ProtectedEndpoint_WithoutAuth_ReturnsUnauthorized(string method, string path)
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
