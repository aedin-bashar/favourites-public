using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Categories.DeleteCategory;

public sealed class DeleteCategoryHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Returns false for missing OR cross-user ids, matching the same
    // ownership-not-found rule used by links and tags.
    public async Task<bool> HandleAsync(
        DeleteCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var category = await dbContext.Categories
            .SingleOrDefaultAsync(
                category => category.Id == command.Id && category.UserId == userId,
                cancellationToken);

        if (category is null)
        {
            return false;
        }

        var assignedLinks = await dbContext.FavouriteLinks
            .Where(link => link.UserId == userId && link.CategoryId == category.Id)
            .ToListAsync(cancellationToken);

        foreach (var link in assignedLinks)
        {
            link.ClearCategory(category.Id);
        }

        var preferences = await dbContext.UserPreferences
            .SingleOrDefaultAsync(
                preferences => preferences.UserId == userId &&
                               preferences.DefaultCategoryId == category.Id,
                cancellationToken);

        preferences?.ClearDefaultCategory(category.Id);

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
