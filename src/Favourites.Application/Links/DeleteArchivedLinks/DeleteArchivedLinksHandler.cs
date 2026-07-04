using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.DeleteArchivedLinks;

public sealed class DeleteArchivedLinksHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    /// <summary>
    /// Permanently deletes every archived link that belongs to the authenticated
    /// user ("empty archive"). Returns the number of links deleted.
    /// </summary>
    public async Task<int> HandleAsync(
        DeleteArchivedLinksCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var archived = await dbContext.FavouriteLinks
            .Where(l => l.UserId == userId && l.IsArchived)
            .ToListAsync(cancellationToken);

        if (archived.Count == 0)
        {
            return 0;
        }

        dbContext.FavouriteLinks.RemoveRange(archived);
        await dbContext.SaveChangesAsync(cancellationToken);

        return archived.Count;
    }
}
