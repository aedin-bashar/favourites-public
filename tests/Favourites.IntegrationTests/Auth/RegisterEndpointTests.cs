using System.Net;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Microsoft.AspNetCore.Http;

namespace Favourites.IntegrationTests.Auth;

public sealed class RegisterEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task Register_WithValidRequest_ReturnsOkWithUserData()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var request = new RegisterRequest(
            DisplayName: "Test User",
            Email: "test@example.com",
            Password: ValidPassword,
            ConfirmPassword: ValidPassword);

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();

        Assert.NotNull(body);
        Assert.Equal(request.Email, body!.Email);
        Assert.Equal(request.DisplayName, body.DisplayName);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Theory]
    [InlineData("", "missing-name@example.com", ValidPassword, ValidPassword, nameof(RegisterRequest.DisplayName))]
    [InlineData("Test User", "", ValidPassword, ValidPassword, nameof(RegisterRequest.Email))]
    [InlineData("Test User", "missing-password@example.com", "", "", nameof(RegisterRequest.Password))]
    [InlineData("Test User", "missing-confirm@example.com", ValidPassword, "", nameof(RegisterRequest.ConfirmPassword))]
    public async Task Register_WithMissingRequiredField_ReturnsValidationError(
        string displayName,
        string email,
        string password,
        string confirmPassword,
        string expectedErrorKey)
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var request = new RegisterRequest(displayName, email, password, confirmPassword);

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(expectedErrorKey, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_WithMismatchedPasswords_ReturnsConfirmPasswordError()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var request = new RegisterRequest(
            DisplayName: "Test User",
            Email: "mismatch@example.com",
            Password: ValidPassword,
            ConfirmPassword: "DifferentPassword123!");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(nameof(RegisterRequest.ConfirmPassword), problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsPasswordError()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var request = new RegisterRequest(
            DisplayName: "Test User",
            Email: "weak-password@example.com",
            Password: "short",
            ConfirmPassword: "short");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(nameof(RegisterRequest.Password), problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsEmailError()
    {
        await using var factory = new FavouritesApiFactory();
        using var client = factory.CreateClient();

        var firstRequest = new RegisterRequest(
            DisplayName: "First User",
            Email: "duplicate@example.com",
            Password: ValidPassword,
            ConfirmPassword: ValidPassword);

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondRequest = firstRequest with { DisplayName = "Second User" };
        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", secondRequest);

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);

        var problem = await secondResponse.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(nameof(RegisterRequest.Email), problem!.Errors.Keys);
    }
}
