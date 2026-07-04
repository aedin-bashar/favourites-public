using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Dashboard.GetDashboardSummary;

public sealed class GetDashboardSummaryHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<DashboardSummaryDto> HandleAsync(
        GetDashboardSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);

        var totalLinks = await dbContext.FavouriteLinks
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId && !l.IsArchived, cancellationToken);

        var totalTags = await dbContext.Tags
            .AsNoTracking()
            .CountAsync(t => t.UserId == userId, cancellationToken);

        var totalCategories = await dbContext.Categories
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId, cancellationToken);

        var totalArchived = await dbContext.FavouriteLinks
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId && l.IsArchived, cancellationToken);

        var linksAdded = await dbContext.FavouriteLinks
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId && !l.IsArchived && l.CreatedAtUtc >= weekAgo, cancellationToken);

        var linksArchived = await dbContext.FavouriteLinks
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId && l.IsArchived && l.UpdatedAtUtc >= weekAgo, cancellationToken);

        var categoriesCreated = await dbContext.Categories
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.CreatedAtUtc >= weekAgo, cancellationToken);

        var tagsCreated = await dbContext.Tags
            .AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.CreatedAtUtc >= weekAgo, cancellationToken);

        return new DashboardSummaryDto(
            TotalLinks: totalLinks,
            TotalTags: totalTags,
            TotalCategories: totalCategories,
            TotalArchived: totalArchived,
            ThisWeek: new DashboardThisWeekDto(
                LinksAdded: linksAdded,
                CategoriesCreated: categoriesCreated,
                TagsCreated: tagsCreated,
                LinksArchived: linksArchived));
    }
}
