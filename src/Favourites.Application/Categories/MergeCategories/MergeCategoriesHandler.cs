using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Categories.MergeCategories;

public sealed class MergeCategoriesHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<int> HandleAsync(
        MergeCategoriesCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var keepExists = await dbContext.Categories
            .AnyAsync(c => c.Id == command.KeepCategoryId && c.UserId == userId, cancellationToken);

        if (!keepExists)
            throw new InvalidOperationException("Target category not found.");

        var mergeIds = command.MergeCategoryIds
            .Where(id => id != command.KeepCategoryId)
            .Distinct()
            .ToList();

        if (mergeIds.Count == 0)
            return 0;

        var mergeCategories = await dbContext.Categories
            .Where(c => mergeIds.Contains(c.Id) && c.UserId == userId)
            .ToListAsync(cancellationToken);

        if (mergeCategories.Count == 0)
            return 0;

        var confirmedMergeIds = mergeCategories.Select(c => c.Id).ToList();

        var linksToReassign = await dbContext.FavouriteLinks
            .Where(l => l.UserId == userId && confirmedMergeIds.Contains(l.CategoryId!.Value))
            .ToListAsync(cancellationToken);

        foreach (var link in linksToReassign)
        {
            link.UpdateContent(
                link.Url,
                link.Title,
                link.Description,
                command.KeepCategoryId);
        }

        dbContext.Categories.RemoveRange(mergeCategories);

        await dbContext.SaveChangesAsync(cancellationToken);

        return mergeCategories.Count;
    }
}
