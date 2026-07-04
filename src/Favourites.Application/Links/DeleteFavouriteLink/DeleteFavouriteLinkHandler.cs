using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.DeleteFavouriteLink;

public sealed class DeleteFavouriteLinkHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Returns true when the link existed and was deleted, false when it did
    // not exist OR belonged to another user (ownership-not-found rule — same
    // pattern as the read-by-id / update handlers; the API maps false to 404
    // so cross-user deletes can't be distinguished from missing ids).
    public async Task<bool> HandleAsync(
        DeleteFavouriteLinkCommand command,
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

        dbContext.FavouriteLinks.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
