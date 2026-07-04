namespace Favourites.Domain.Entities;

public sealed class Tag
{
    public const int MaxNameLength = 50;

    private Tag()
    {
    }

    private Tag(Guid id, Guid userId, string name, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Tag Create(Guid userId, string name, DateTimeOffset? createdAtUtc = null)
    {
        EnsureValidOwner(userId);
        var normalizedName = EnsureValidName(name);

        return new Tag(
            id: Guid.NewGuid(),
            userId: userId,
            name: normalizedName,
            createdAtUtc: createdAtUtc ?? DateTimeOffset.UtcNow);
    }

    public void UpdateName(string name)
    {
        Name = EnsureValidName(name);
    }

    private static void EnsureValidOwner(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A tag must belong to a user.", nameof(userId));
        }
    }

    private static string EnsureValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name is required.", nameof(name));
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Tag name must be {MaxNameLength} characters or fewer.",
                nameof(name));
        }

        return trimmed;
    }
}
