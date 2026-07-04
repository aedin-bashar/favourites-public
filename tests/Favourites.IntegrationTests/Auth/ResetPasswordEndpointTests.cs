using System.Net;
using System.Net.Http.Json;
using System.Web;
using Favourites.Api.Contracts.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Auth;

public sealed class ResetPasswordEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";
    private const string NewPassword = "NewPassword456!";

    private static string ExtractTokenFromEmail(string htmlBody)
    {
        // The reset link looks like: href="...://.../reset-password?token=ENCODED_TOKEN"
        var marker = "reset-password?token=";
        var start = htmlBody.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("Token URL not found in email body.");
        var tokenStart = start + marker.Length;
        var tokenEnd = htmlBody.IndexOf('"', tokenStart);
        var encodedToken = tokenEnd < 0
            ? htmlBody[tokenStart..]
            : htmlBody[tokenStart..tokenEnd];
        return HttpUtility.UrlDecode(encodedToken);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_Returns200AndChangesPassword()
    {
        var emailSender = new CapturingEmailSender();
        await using var factory = new PasswordResetApiFactory(emailSender);
        using var client = factory.CreateClient();

        var email = "reset-valid@example.com";
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email, ValidPassword, ValidPassword));

        await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email));

        var rawToken = ExtractTokenFromEmail(emailSender.SentMessages[0].HtmlBody);

        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(rawToken, NewPassword, NewPassword));

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        // Log out and verify login with new password works.
        await client.PostAsJsonAsync("/api/auth/logout", new { });
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, NewPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_Returns400()
    {
        var emailSender = new CapturingEmailSender();
        await using var factory = new PasswordResetApiFactory(emailSender);
        using var client = factory.CreateClient();

        var email = "reset-expired@example.com";
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email, ValidPassword, ValidPassword));

        await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email));

        var rawToken = ExtractTokenFromEmail(emailSender.SentMessages[0].HtmlBody);

        // Manually expire the token in the DB.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Favourites.Infrastructure.Persistence.FavouritesDbContext>();
        var record = db.PasswordResetTokens.Single();
        // Override ExpiresAtUtc to the past — use direct property set via EF tracking.
        db.Entry(record).Property("ExpiresAtUtc").CurrentValue = DateTime.UtcNow.AddHours(-2);
        await db.SaveChangesAsync();

        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(rawToken, NewPassword, NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, resetResponse.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithAlreadyUsedToken_Returns400()
    {
        var emailSender = new CapturingEmailSender();
        await using var factory = new PasswordResetApiFactory(emailSender);
        using var client = factory.CreateClient();

        var email = "reset-used@example.com";
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email, ValidPassword, ValidPassword));

        await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email));

        var rawToken = ExtractTokenFromEmail(emailSender.SentMessages[0].HtmlBody);

        // Use the token once.
        var firstReset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(rawToken, NewPassword, NewPassword));
        Assert.Equal(HttpStatusCode.OK, firstReset.StatusCode);

        // Try to use it again.
        var secondReset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(rawToken, NewPassword, NewPassword));
        Assert.Equal(HttpStatusCode.BadRequest, secondReset.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        await using var factory = new PasswordResetApiFactory(new CapturingEmailSender());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest("invalid-garbage-token", NewPassword, NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithMismatchedPasswords_Returns400ValidationError()
    {
        await using var factory = new PasswordResetApiFactory(new CapturingEmailSender());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest("some-token", NewPassword, "Different123!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(ResetPasswordRequest.ConfirmNewPassword), problem!.Errors.Keys);
    }
}
