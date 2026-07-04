using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.ArchiveFavouriteLink;

public sealed class ArchiveFavouriteLinkHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Archive a link the authenticated user owns.
    //
    // Returns true when the link existed and was reached (already-archived
    // links also return true so the action is idempotent from the caller's
    // perspective). Returns false when the link does not exist OR belongs to
    // another user (ownership-not-found rule — the API maps false to 404 so
    // cross-user archives can't be distinguished from missing ids).
    public async Task<bool> HandleAsync(
        ArchiveFavouriteLinkCommand command,
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

        if (link.Archive())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
