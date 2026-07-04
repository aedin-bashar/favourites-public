namespace Favourites.Application.Users.Dtos;

public sealed record UserPreferencesDto(
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
    bool ProductUpdates,
    DateTimeOffset UpdatedAtUtc);
