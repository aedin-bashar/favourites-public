namespace Favourites.Api.Contracts.User;

public sealed record UserProfileResponse(
    Guid Id,
    string DisplayName,
    string Email);

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record UserPreferencesResponse(
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

public sealed record PatchUserPreferencesRequest(
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
