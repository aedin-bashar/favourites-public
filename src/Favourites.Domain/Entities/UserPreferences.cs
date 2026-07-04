namespace Favourites.Domain.Entities;

public sealed class UserPreferences
{
    public const string ThemeLight = "light";
    public const string ThemeDark = "dark";
    public const string ThemeSystem = "system";
    public const string DensityComfortable = "comfortable";
    public const string DensityCompact = "compact";
    public const string TagsSortName = "name";
    public const string TagsSortMostUsed = "most-used";
    public const string TagsSortNewest = "newest";
    public const string CategoriesSortName = "name";
    public const string CategoriesSortLargest = "largest";
    public const string CategoriesSortNewest = "newest";
    public const string CategoriesSortRecentlyActive = "recently-active";

    private UserPreferences()
    {
    }

    private UserPreferences(Guid userId, DateTimeOffset updatedAtUtc)
    {
        UserId = EnsureValidOwner(userId);
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid UserId { get; private set; }

    public string Theme { get; private set; } = ThemeLight;

    public string Density { get; private set; } = DensityComfortable;

    public Guid? DefaultCategoryId { get; private set; }

    public bool AutoExtractTitle { get; private set; } = true;

    public bool ShowFavicon { get; private set; } = true;

    public bool OpenInNewTab { get; private set; } = true;

    public bool ConfirmBeforeDelete { get; private set; } = true;

    public bool SuggestTagsAutomatically { get; private set; } = true;

    public bool ShowColorsOnTagChips { get; private set; } = true;

    public string TagsDefaultSort { get; private set; } = TagsSortName;

    public string CategoriesDefaultSort { get; private set; } = CategoriesSortName;

    public bool WeeklySummaryEmail { get; private set; }

    public bool SecurityAlerts { get; private set; } = true;

    public bool ProductUpdates { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static UserPreferences CreateDefault(Guid userId, DateTimeOffset? updatedAtUtc = null) =>
        new(userId, updatedAtUtc ?? DateTimeOffset.UtcNow);

    public void Update(
        string theme,
        string density,
        Guid? defaultCategoryId,
        bool autoExtractTitle,
        bool showFavicon,
        bool openInNewTab,
        bool confirmBeforeDelete,
        bool suggestTagsAutomatically,
        bool showColorsOnTagChips,
        string tagsDefaultSort,
        string categoriesDefaultSort,
        bool weeklySummaryEmail,
        bool securityAlerts,
        bool productUpdates,
        DateTimeOffset? updatedAtUtc = null)
    {
        Theme = EnsureAllowed(theme, [ThemeLight, ThemeDark, ThemeSystem], nameof(theme));
        Density = EnsureAllowed(density, [DensityComfortable, DensityCompact], nameof(density));
        DefaultCategoryId = EnsureValidOptionalId(defaultCategoryId, nameof(defaultCategoryId));
        AutoExtractTitle = autoExtractTitle;
        ShowFavicon = showFavicon;
        OpenInNewTab = openInNewTab;
        ConfirmBeforeDelete = confirmBeforeDelete;
        SuggestTagsAutomatically = suggestTagsAutomatically;
        ShowColorsOnTagChips = showColorsOnTagChips;
        TagsDefaultSort = EnsureAllowed(
            tagsDefaultSort,
            [TagsSortName, TagsSortMostUsed, TagsSortNewest],
            nameof(tagsDefaultSort));
        CategoriesDefaultSort = EnsureAllowed(
            categoriesDefaultSort,
            [CategoriesSortName, CategoriesSortLargest, CategoriesSortNewest, CategoriesSortRecentlyActive],
            nameof(categoriesDefaultSort));
        WeeklySummaryEmail = weeklySummaryEmail;
        SecurityAlerts = securityAlerts;
        ProductUpdates = productUpdates;
        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public void ClearDefaultCategory(Guid categoryId, DateTimeOffset? updatedAtUtc = null)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Category id must not be empty when provided.",
                nameof(categoryId));
        }

        if (DefaultCategoryId != categoryId)
        {
            return;
        }

        DefaultCategoryId = null;
        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
    }

    private static Guid EnsureValidOwner(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Preferences must belong to a user.", nameof(userId));
        }

        return userId;
    }

    private static Guid? EnsureValidOptionalId(Guid? id, string paramName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Optional preference IDs must not be empty.", paramName);
        }

        return id;
    }

    private static string EnsureAllowed(string value, IReadOnlyCollection<string> allowed, string paramName)
    {
        var normalized = value.Trim().ToLowerInvariant();

        if (!allowed.Contains(normalized))
        {
            throw new ArgumentException("Unsupported preference value.", paramName);
        }

        return normalized;
    }
}
