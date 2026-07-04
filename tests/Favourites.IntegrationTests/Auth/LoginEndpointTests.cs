using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Microsoft.AspNetCore.Http;

namespace Favourites.IntegrationTests.Auth;

public sealed class LoginEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";
    private const string IdentityCookieName = ".AspNetCore.Identity.Application";

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkAndSetsAuthCookie()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        const string email = "login-success@example.com";
        const string displayName = "Login Success";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(displayName, email, ValidPassword, ValidPassword));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, ValidPassword));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);
        Assert.Equal(email, body!.Email);
        Assert.Equal(displayName, body.DisplayName);
        Assert.NotEqual(Guid.Empty, body.Id);

        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        var identityCookie = GetIdentityCookie(cookies!);
        Assert.DoesNotContain("expires=", identityCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithRememberMeTrue_SetsPersistentAuthCookie()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        const string email = "login-remember@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Remember User", email, ValidPassword, ValidPassword));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, ValidPassword, RememberMe: true));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));

        var identityCookie = GetIdentityCookie(cookies!);
        Assert.Contains("expires=", identityCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", ValidPassword, nameof(LoginRequest.Email))]
    [InlineData("missing-password@example.com", "", nameof(LoginRequest.Password))]
    public async Task Login_WithMissingRequiredField_ReturnsValidationError(
        string email,
        string password,
        string expectedErrorKey)
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(expectedErrorKey, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorizedAndSetsNoAuthCookie()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("never-registered@example.com", ValidPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNoIdentityCookie(response);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorizedAndSetsNoAuthCookie()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        const string email = "wrong-password@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email, ValidPassword, ValidPassword));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "WrongPassword123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        AssertNoIdentityCookie(loginResponse);
    }

    private static void AssertNoIdentityCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return;
        }

        Assert.DoesNotContain(cookies, cookie =>
            cookie.Contains(IdentityCookieName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetIdentityCookie(IEnumerable<string> cookies) =>
        Assert.Single(
            cookies,
            cookie => cookie.Contains(IdentityCookieName, StringComparison.OrdinalIgnoreCase));
}
