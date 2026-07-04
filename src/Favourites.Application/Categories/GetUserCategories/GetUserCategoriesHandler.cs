using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Categories.GetUserCategories;

public sealed class GetUserCategoriesHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<IReadOnlyList<CategoryDto>> HandleAsync(
        GetUserCategoriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.UserId == userId)
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);

        var categoryIds = categories.Select(category => category.Id).ToArray();

        var usage = categoryIds.Length == 0
            ? new Dictionary<Guid, CategoryUsage>()
            : await dbContext.FavouriteLinks
                .AsNoTracking()
                .Where(link => link.UserId == userId
                    && link.CategoryId != null
                    && categoryIds.Contains(link.CategoryId.Value))
                .GroupBy(link => link.CategoryId!.Value)
                .Select(group => new
                {
                    CategoryId = group.Key,
                    Count = group.Count(),
                    LastActivityAtUtc = group.Max(link => link.UpdatedAtUtc ?? link.CreatedAtUtc),
                })
                .ToDictionaryAsync(
                    item => item.CategoryId,
                    item => new CategoryUsage(item.Count, item.LastActivityAtUtc),
                    cancellationToken);

        return categories
            .Select(category =>
            {
                usage.TryGetValue(category.Id, out var categoryUsage);
                return new CategoryDto(
                    Id: category.Id,
                    Name: category.Name,
                    Color: category.Color,
                    LinkCount: categoryUsage?.Count ?? 0,
                    CreatedAtUtc: category.CreatedAtUtc,
                    LastActivityAtUtc: categoryUsage?.LastActivityAtUtc);
            })
            .ToList();
    }

    private sealed record CategoryUsage(int Count, DateTimeOffset LastActivityAtUtc);
}
