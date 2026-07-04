using Favourites.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace Favourites.Infrastructure.Identity;

public sealed class PasswordResetService(UserManager<ApplicationUser> userManager) : IPasswordResetService
{
    public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        return user?.Id;
    }

    public async Task<bool> UpdatePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return false;

        var passwordHash = userManager.PasswordHasher.HashPassword(user, newPassword);
        user.PasswordHash = passwordHash;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded;
    }
}
