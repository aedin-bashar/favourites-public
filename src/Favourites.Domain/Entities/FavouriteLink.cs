namespace Favourites.Domain.Entities;

public sealed class FavouriteLink
{
    public const int MaxUrlLength = 2048;
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;

    private FavouriteLink()
    {
    }

    private FavouriteLink(
        Guid id,
        Guid userId,
        string url,
        string title,
        string? description,
        Guid? categoryId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Url = url;
        Title = title;
        Description = description;
        CategoryId = categoryId;
        IsArchived = false;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = null;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Url { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid? CategoryId { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static FavouriteLink Create(
        Guid userId,
        string url,
        string title,
        string? description,
        Guid? categoryId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        EnsureValidOwner(userId);
        var normalizedUrl = EnsureValidUrl(url);
        var normalizedTitle = EnsureValidTitle(title);
        var normalizedDescription = EnsureValidDescription(description);
        var normalizedCategoryId = EnsureValidCategory(categoryId);

        return new FavouriteLink(
            id: Guid.NewGuid(),
            userId: userId,
            url: normalizedUrl,
            title: normalizedTitle,
            description: normalizedDescription,
            categoryId: normalizedCategoryId,
            createdAtUtc: createdAtUtc ?? DateTimeOffset.UtcNow);
    }

    public void UpdateContent(
        string url,
        string title,
        string? description,
        Guid? categoryId = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        Url = EnsureValidUrl(url);
        Title = EnsureValidTitle(title);
        Description = EnsureValidDescription(description);
        CategoryId = EnsureValidCategory(categoryId);
        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
    }

    // Archives the link if it is currently active. Returns true
    // when a state change happened; false when the link was already archived
    // so callers can skip the SaveChanges round-trip.
    public bool Archive(DateTimeOffset? updatedAtUtc = null)
    {
        if (IsArchived)
        {
            return false;
        }

        IsArchived = true;
        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
        return true;
    }

    // Restores an archived link to active state. Mirrors Archive:
    // returns true on state change, false when the link was already active.
    public bool Restore(DateTimeOffset? updatedAtUtc = null)
    {
        if (!IsArchived)
        {
            return false;
        }

        IsArchived = false;
        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
        return true;
    }

    public void ClearCategory(Guid categoryId, DateTimeOffset? updatedAtUtc = null)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Category id must not be empty when provided.",
                nameof(categoryId));
        }

        if (CategoryId != categoryId)
        {
            return;
        }

        CategoryId = null;
        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
    }

    private static Guid? EnsureValidCategory(Guid? categoryId)
    {
        if (categoryId is null)
        {
            return null;
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Category id must not be empty when provided.",
                nameof(categoryId));
        }

        return categoryId;
    }

    private static void EnsureValidOwner(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A favourite link must belong to a user.", nameof(userId));
        }
    }

    private static string EnsureValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required.", nameof(url));
        }

        var trimmed = url.Trim();

        if (trimmed.Length > MaxUrlLength)
        {
            throw new ArgumentException(
                $"URL must be {MaxUrlLength} characters or fewer.",
                nameof(url));
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "URL must be a valid absolute http or https URL.",
                nameof(url));
        }

        return parsed.ToString();
    }

    private static string EnsureValidTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        var trimmed = title.Trim();

        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Title must be {MaxTitleLength} characters or fewer.",
                nameof(title));
        }

        return trimmed;
    }

    private static string? EnsureValidDescription(string? description)
    {
        if (description is null)
        {
            return null;
        }

        var trimmed = description.Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"Description must be {MaxDescriptionLength} characters or fewer.",
                nameof(description));
        }

        return trimmed;
    }
}
