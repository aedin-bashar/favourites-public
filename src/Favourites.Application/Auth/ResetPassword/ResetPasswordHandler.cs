using System.Security.Cryptography;
using System.Text;
using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Auth.ResetPassword;

public sealed class ResetPasswordHandler(
    IPasswordResetService passwordResetService,
    IFavouritesDbContext dbContext)
{
    public async Task<ResetPasswordResult> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var tokenHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(command.Token)));

        var record = await dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (record is null)
            return new ResetPasswordResult(false, "The reset link is invalid or has expired.");

        if (record.ExpiresAtUtc < DateTime.UtcNow)
            return new ResetPasswordResult(false, "The reset link has expired. Request a new one.");

        if (record.UsedAtUtc is not null)
            return new ResetPasswordResult(false, "This reset link has already been used.");

        var succeeded = await passwordResetService.UpdatePasswordAsync(record.UserId, command.NewPassword, cancellationToken);
        if (!succeeded)
            return new ResetPasswordResult(false, "Failed to update password. The new password may not meet the requirements.");

        record.UsedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResetPasswordResult(true);
    }
}
