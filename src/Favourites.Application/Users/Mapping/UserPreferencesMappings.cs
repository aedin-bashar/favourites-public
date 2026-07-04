using Favourites.Application.Users.Dtos;
using Favourites.Domain.Entities;

namespace Favourites.Application.Users.Mapping;

public static class UserPreferencesMappings
{
    public static UserPreferencesDto ToDto(this UserPreferences preferences) =>
        new(
            preferences.Theme,
            preferences.Density,
            preferences.DefaultCategoryId,
            preferences.AutoExtractTitle,
            preferences.ShowFavicon,
            preferences.OpenInNewTab,
            preferences.ConfirmBeforeDelete,
            preferences.SuggestTagsAutomatically,
            preferences.ShowColorsOnTagChips,
            preferences.TagsDefaultSort,
            preferences.CategoriesDefaultSort,
            preferences.WeeklySummaryEmail,
            preferences.SecurityAlerts,
            preferences.ProductUpdates,
            preferences.UpdatedAtUtc);
}
