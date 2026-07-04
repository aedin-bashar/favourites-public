namespace Favourites.Application.Abstractions.Identity;

public interface IUserAccountDeletionService
{
    Task<bool> DeleteCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
