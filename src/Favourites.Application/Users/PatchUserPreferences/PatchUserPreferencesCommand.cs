namespace Favourites.Application.Users.PatchUserPreferences;

public sealed record PatchUserPreferencesCommand(
    string Theme,
    string Density,
    Guid? DefaultCategoryId,
    bool AutoExtractTitle,
    bool ShowFavicon,
    bool OpenInNewTab,
    bool ConfirmBeforeDelete,
    bool SuggestTagsAutomatically,
    bool ShowColorsOnTagChips,
    string TagsDefaultSort,
    string CategoriesDefaultSort,
    bool WeeklySummaryEmail,
    bool SecurityAlerts,
    bool ProductUpdates);
