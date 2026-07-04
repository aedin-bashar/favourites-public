using Favourites.Application.Categories.Dtos;
using Favourites.Application.Links.Dtos;
using Favourites.Application.Tags.Dtos;
using Favourites.Domain.Entities;

namespace Favourites.Application.Links.Mapping;

public static class FavouriteLinkMappings
{
    public static FavouriteLinkDto ToDto(
        this FavouriteLink link,
        IReadOnlyList<TagDto>? tags = null,
        CategoryDto? category = null) =>
        new(
            link.Id,
            link.Url,
            link.Title,
            link.Description,
            link.IsArchived,
            link.CreatedAtUtc,
            link.UpdatedAtUtc,
            tags ?? Array.Empty<TagDto>(),
            category);
}
