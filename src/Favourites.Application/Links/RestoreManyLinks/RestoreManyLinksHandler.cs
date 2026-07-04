using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.RestoreManyLinks;

public sealed class RestoreManyLinksHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    /// <summary>
    /// Restores all archived links in <paramref name="command.LinkIds"/> that
    /// belong to the authenticated user. Links that do not exist or belong to
    /// another user are silently skipped (ownership rule). Returns the count
    /// of links that were actually restored.
    /// </summary>
    public async Task<int> HandleAsync(
        RestoreManyLinksCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        if (command.LinkIds.Count == 0)
        {
            return 0;
        }

        var links = await dbContext.FavouriteLinks
            .Where(l => command.LinkIds.Contains(l.Id) && l.UserId == userId && l.IsArchived)
            .ToListAsync(cancellationToken);

        var restored = 0;
        foreach (var link in links)
        {
            if (link.Restore())
            {
                restored++;
            }
        }

        if (restored > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return restored;
    }
}
