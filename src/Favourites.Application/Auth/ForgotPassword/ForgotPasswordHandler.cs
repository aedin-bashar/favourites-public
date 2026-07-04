using System.Security.Cryptography;
using System.Text;
using Favourites.Application.Abstractions.Email;
using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Domain.Entities;

namespace Favourites.Application.Auth.ForgotPassword;

public sealed class ForgotPasswordHandler(
    IPasswordResetService passwordResetService,
    IFavouritesDbContext dbContext,
    IEmailSender emailSender)
{
    public async Task HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var userId = await passwordResetService.FindUserIdByEmailAsync(command.Email.Trim(), cancellationToken);

        // Always return success to prevent user enumeration.
        if (userId is null) return;

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var resetToken = new PasswordResetToken
        {
            UserId = userId.Value,
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var resetUrl = $"{command.ResetBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var html = $"""
            <p>Hi,</p>
            <p>You requested to reset your Favourites password. Click the link below to choose a new password. The link expires in 1 hour.</p>
            <p><a href="{resetUrl}">Reset my password</a></p>
            <p>If you did not request this, you can safely ignore this email.</p>
            """;

        // The reset token has already been persisted, so the email must go out
        // regardless of what happens to this HTTP request — pass CancellationToken.None.
        // The production IEmailSender queues delivery on a background worker, keeping
        // the response time independent of SMTP (and of whether the account exists).
        await emailSender.SendAsync(command.Email, "Reset your password", html, CancellationToken.None);
    }
}
