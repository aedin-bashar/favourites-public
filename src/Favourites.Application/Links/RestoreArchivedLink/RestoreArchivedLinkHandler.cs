using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.RestoreArchivedLink;

public sealed class RestoreArchivedLinkHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Restore an archived link back to active state.
    //
    // Returns true when the link existed and was reached (already-active
    // links also return true so the action is idempotent). Returns false
    // when the link does not exist OR belongs to another user
    // (ownership-not-found rule — the API maps false to 404).
    public async Task<bool> HandleAsync(
        RestoreArchivedLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var link = await dbContext.FavouriteLinks
            .SingleOrDefaultAsync(
                link => link.Id == command.Id && link.UserId == userId,
                cancellationToken);

        if (link is null)
        {
            return false;
        }

        if (link.Restore())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
