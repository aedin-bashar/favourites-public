namespace Favourites.Domain.Entities;

public sealed class Category
{
    public const int MaxNameLength = 50;
    public const int ColorLength = 7;

    public static readonly string[] Palette =
    [
        "#0d6efd", "#6610f2", "#d63384", "#dc3545",
        "#fd7e14", "#198754", "#0dcaf0", "#6f42c1",
    ];

    private Category()
    {
    }

    private Category(Guid id, Guid userId, string name, string color, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Name = name;
        Color = color;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Color { get; private set; } = Palette[0];

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Category Create(Guid userId, string name, DateTimeOffset? createdAtUtc = null)
    {
        EnsureValidOwner(userId);
        var normalizedName = EnsureValidName(name);
        var color = Palette[Math.Abs(normalizedName.GetHashCode()) % Palette.Length];

        return new Category(
            id: Guid.NewGuid(),
            userId: userId,
            name: normalizedName,
            color: color,
            createdAtUtc: createdAtUtc ?? DateTimeOffset.UtcNow);
    }

    public void UpdateName(string name)
    {
        Name = EnsureValidName(name);
    }

    public void UpdateColor(string color)
    {
        Color = string.IsNullOrWhiteSpace(color) ? Palette[0] : color.Trim();
    }

    private static void EnsureValidOwner(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A category must belong to a user.", nameof(userId));
        }
    }

    private static string EnsureValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Category name must be {MaxNameLength} characters or fewer.",
                nameof(name));
        }

        return trimmed;
    }
}
