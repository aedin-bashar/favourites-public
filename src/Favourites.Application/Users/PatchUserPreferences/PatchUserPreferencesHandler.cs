using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Users.Dtos;
using Favourites.Application.Users.Mapping;
using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Users.PatchUserPreferences;

public sealed class PatchUserPreferencesHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<UserPreferencesDto?> HandleAsync(
        PatchUserPreferencesCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        if (command.DefaultCategoryId is { } categoryId)
        {
            var ownsCategory = await dbContext.Categories.AnyAsync(
                category => category.Id == categoryId && category.UserId == userId,
                cancellationToken);

            if (!ownsCategory)
            {
                return null;
            }
        }

        var preferences = await dbContext.UserPreferences
            .SingleOrDefaultAsync(preferences => preferences.UserId == userId, cancellationToken);

        if (preferences is null)
        {
            preferences = UserPreferences.CreateDefault(userId);
            dbContext.UserPreferences.Add(preferences);
        }

        preferences.Update(
            command.Theme,
            command.Density,
            command.DefaultCategoryId,
            command.AutoExtractTitle,
            command.ShowFavicon,
            command.OpenInNewTab,
            command.ConfirmBeforeDelete,
            command.SuggestTagsAutomatically,
            command.ShowColorsOnTagChips,
            command.TagsDefaultSort,
            command.CategoriesDefaultSort,
            command.WeeklySummaryEmail,
            command.SecurityAlerts,
            command.ProductUpdates);

        await dbContext.SaveChangesAsync(cancellationToken);

        return preferences.ToDto();
    }
}
