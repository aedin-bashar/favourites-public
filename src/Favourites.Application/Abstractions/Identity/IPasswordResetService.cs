namespace Favourites.Application.Abstractions.Identity;

public interface IPasswordResetService
{
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);
}
