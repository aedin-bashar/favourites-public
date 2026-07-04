using Favourites.Application.Tags.Dtos;
using Favourites.Domain.Entities;

namespace Favourites.Application.Tags.Mapping;

public static class TagMappings
{
    public static TagDto ToDto(this Tag tag) => new(
        Id: tag.Id,
        Name: tag.Name,
        CreatedAtUtc: tag.CreatedAtUtc);
}
