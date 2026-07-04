namespace Favourites.Application.Categories.Dtos;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Color = "#6c757d",
    int LinkCount = 0,
    DateTimeOffset CreatedAtUtc = default,
    DateTimeOffset? LastActivityAtUtc = null);
