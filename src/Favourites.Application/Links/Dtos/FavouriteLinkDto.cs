using Favourites.Application.Categories.Dtos;
using Favourites.Application.Tags.Dtos;

namespace Favourites.Application.Links.Dtos;

public sealed record FavouriteLinkDto(
    Guid Id,
    string Url,
    string Title,
    string? Description,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<TagDto> Tags,
    CategoryDto? Category);
