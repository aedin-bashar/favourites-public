using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Tags.MergeTags;

public sealed class MergeTagsHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<int> HandleAsync(
        MergeTagsCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var keepTag = await dbContext.Tags
            .FirstOrDefaultAsync(t => t.Id == command.KeepTagId && t.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Target tag not found.");

        var mergeIds = command.MergeTagIds
            .Where(id => id != command.KeepTagId)
            .Distinct()
            .ToList();

        if (mergeIds.Count == 0)
            return 0;

        var mergeTags = await dbContext.Tags
            .Where(t => mergeIds.Contains(t.Id) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        if (mergeTags.Count == 0)
            return 0;

        var confirmedMergeIds = mergeTags.Select(t => t.Id).ToList();

        // Links already linked to the keep-tag (to avoid duplicate join records).
        var alreadyLinked = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(lt => lt.TagId == command.KeepTagId)
            .Select(lt => lt.FavouriteLinkId)
            .ToHashSetAsync(cancellationToken);

        var rowsToReassign = await dbContext.FavouriteLinkTags
            .Where(lt => confirmedMergeIds.Contains(lt.TagId))
            .ToListAsync(cancellationToken);

        foreach (var row in rowsToReassign)
        {
            if (alreadyLinked.Contains(row.FavouriteLinkId))
            {
                // Link already tagged with keep-tag — just remove the duplicate.
                dbContext.FavouriteLinkTags.Remove(row);
            }
            else
            {
                // Composite-PK rows cannot change key values — remove and re-add with the keep-tag id.
                dbContext.FavouriteLinkTags.Remove(row);
                dbContext.FavouriteLinkTags.Add(
                    Favourites.Domain.Entities.FavouriteLinkTag.Create(row.FavouriteLinkId, command.KeepTagId));
                alreadyLinked.Add(row.FavouriteLinkId);
            }
        }

        dbContext.Tags.RemoveRange(mergeTags);

        await dbContext.SaveChangesAsync(cancellationToken);

        return mergeTags.Count;
    }
}
