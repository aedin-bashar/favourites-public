using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Tags.GetTagsSummary;

public sealed class GetTagsSummaryHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<TagsSummaryDto> HandleAsync(
        GetTagsSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var totalTags = await dbContext.Tags
            .AsNoTracking()
            .CountAsync(t => t.UserId == userId, cancellationToken);

        var usedTagIds = dbContext.FavouriteLinkTags
            .Join(
                dbContext.FavouriteLinks,
                lt => lt.FavouriteLinkId,
                l => l.Id,
                (lt, l) => new { lt.TagId, l.UserId })
            .Where(x => x.UserId == userId)
            .Select(x => x.TagId)
            .Distinct();

        var unusedTags = await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId && !usedTagIds.Contains(t.Id))
            .CountAsync(cancellationToken);

        var mostUsed = await dbContext.FavouriteLinkTags
            .Join(
                dbContext.FavouriteLinks,
                lt => lt.FavouriteLinkId,
                l => l.Id,
                (lt, l) => new { lt.TagId, l.UserId })
            .Where(x => x.UserId == userId)
            .GroupBy(x => x.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Join(
                dbContext.Tags,
                x => x.TagId,
                t => t.Id,
                (x, t) => new { t.Id, t.Name, x.Count })
            .FirstOrDefaultAsync(cancellationToken);

        var recentlyAdded = await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(cancellationToken);

        // Possible duplicates: tags whose lowercase name matches another tag (case-insensitive)
        var tagNames = await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => t.Name.ToLower())
            .ToListAsync(cancellationToken);

        var possibleDuplicates = tagNames
            .GroupBy(n => n)
            .Count(g => g.Count() > 1);

        return new TagsSummaryDto(
            TotalTags: totalTags,
            UnusedTags: unusedTags,
            MostUsed: mostUsed is null
                ? null
                : new TagMostUsedDto(mostUsed.Id, mostUsed.Name, mostUsed.Count),
            RecentlyAdded: recentlyAdded is null
                ? null
                : new TagSummaryItemDto(recentlyAdded.Id, recentlyAdded.Name),
            PossibleDuplicates: possibleDuplicates);
    }
}
