using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Categories.GetCategoriesSummary;

public sealed class GetCategoriesSummaryHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<CategoriesSummaryDto> HandleAsync(
        GetCategoriesSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var totalCategories = await dbContext.Categories
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId, cancellationToken);

        // Categories that have no links assigned to them
        var emptyCategories = await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId
                && !dbContext.FavouriteLinks.Any(l => l.CategoryId == c.Id && l.UserId == userId))
            .CountAsync(cancellationToken);

        // Largest category: the category with the most links
        var largest = await dbContext.FavouriteLinks
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.CategoryId != null)
            .GroupBy(l => l.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Join(
                dbContext.Categories,
                x => x.CategoryId,
                c => c.Id,
                (x, c) => new { c.Id, c.Name, x.Count })
            .FirstOrDefaultAsync(cancellationToken);

        // Most recently added category
        var recentlyAdded = await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var uncategorizedLinks = await dbContext.FavouriteLinks
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId && l.CategoryId == null, cancellationToken);

        return new CategoriesSummaryDto(
            TotalCategories: totalCategories,
            EmptyCategories: emptyCategories,
            LargestCategory: largest is null
                ? null
                : new CategoryLargestDto(largest.Id, largest.Name, largest.Count),
            RecentlyAdded: recentlyAdded is null
                ? null
                : new CategorySummaryItemDto(recentlyAdded.Id, recentlyAdded.Name),
            UncategorizedLinks: uncategorizedLinks);
    }
}
