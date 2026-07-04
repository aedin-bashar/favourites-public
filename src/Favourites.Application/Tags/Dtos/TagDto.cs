namespace Favourites.Application.Tags.Dtos;

public sealed record TagDto(
    Guid Id,
    string Name,
    int LinkCount = 0,
    DateTimeOffset CreatedAtUtc = default,
    DateTimeOffset? LastUsedAtUtc = null);
