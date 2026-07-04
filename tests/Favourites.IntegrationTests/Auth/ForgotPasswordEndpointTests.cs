using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Favourites.Api.Contracts.Auth;
using Favourites.Application.Abstractions.Email;
using Favourites.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.IntegrationTests.Auth;

public sealed class ForgotPasswordEndpointTests
{
    private const string ValidPassword = "ValidPassword123!";

    [Fact]
    public async Task ForgotPassword_WithKnownEmail_Returns200AndStoresToken()
    {
        var emailSender = new CapturingEmailSender();
        await using var factory = new PasswordResetApiFactory(emailSender);
        using var client = factory.CreateClient();

        // Register a user first.
        var email = "forgot-known@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "Test User", email, ValidPassword, ValidPassword));

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(emailSender.SentMessages);
        Assert.Equal(email, emailSender.SentMessages[0].To);
        Assert.Contains("reset-password", emailSender.SentMessages[0].HtmlBody);
        Assert.False(emailSender.SentMessages[0].CancellationToken.CanBeCanceled);

        // Token must be stored in DB.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        var tokenCount = await db.PasswordResetTokens.CountAsync();
        Assert.Equal(1, tokenCount);
    }

    [Fact]
    public async Task ForgotPassword_WithForwardedPrefix_UsesMountedAppPathInResetEmail()
    {
        var emailSender = new CapturingEmailSender();
        await using var factory = new PasswordResetApiFactory(emailSender);
        using var client = factory.CreateClient();

        var email = "forgot-forwarded-prefix@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "Test User", email, ValidPassword, ValidPassword));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest(email))
        };
        request.Headers.Add("X-Forwarded-Prefix", "/projects/favourites");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(emailSender.SentMessages);
        Assert.Contains(
            "/projects/favourites/reset-password?token=",
            emailSender.SentMessages[0].HtmlBody);
    }

    [Fact]
    public async Task ForgotPassword_WithMountedReferer_UsesMountedAppPathInResetEmail()
    {
        var emailSender = new CapturingEmailSender();
        await using var factory = new PasswordResetApiFactory(emailSender);
        using var client = factory.CreateClient();

        var email = "forgot-mounted-referer@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "Test User", email, ValidPassword, ValidPassword));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest(email))
        };
        request.Headers.Referrer = new Uri("http://localhost/projects/favourites/forgot-password");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(emailSender.SentMessages);
        Assert.Contains(
            "/projects/favourites/reset-password?token=",
            emailSender.SentMessages[0].HtmlBody);
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_Returns200AndNoTokenStored()
    {
        var emailSender = new CapturingEmailSender();
        await using var factory = new PasswordResetApiFactory(emailSender);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("nobody@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(emailSender.SentMessages);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FavouritesDbContext>();
        Assert.Equal(0, await db.PasswordResetTokens.CountAsync());
    }

    [Fact]
    public async Task ForgotPassword_WithEmptyEmail_Returns200()
    {
        var emailSender = new CapturingEmailSender();
        await using var factory = new PasswordResetApiFactory(emailSender);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(emailSender.SentMessages);
    }
}

internal sealed class PasswordResetApiFactory(CapturingEmailSender emailSender)
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"favourites-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            var dbContextOptions = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FavouritesDbContext>));
            if (dbContextOptions is not null) services.Remove(dbContextOptions);

            var inMemoryEfServices = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<FavouritesDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .UseInternalServiceProvider(inMemoryEfServices));

            // Replace IEmailSender with the capturing stub.
            var emailSenderDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailSender));
            if (emailSenderDescriptor is not null) services.Remove(emailSenderDescriptor);
            services.AddTransient<IEmailSender>(_ => emailSender);
        });
    }
}

internal sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<(string To, string Subject, string HtmlBody, CancellationToken CancellationToken)> _messages = [];

    public IReadOnlyList<(string To, string Subject, string HtmlBody, CancellationToken CancellationToken)> SentMessages => _messages;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _messages.Add((to, subject, htmlBody, cancellationToken));
        return Task.CompletedTask;
    }
}
