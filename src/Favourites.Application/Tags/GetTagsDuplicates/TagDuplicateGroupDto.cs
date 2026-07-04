using Favourites.Application.Tags.Dtos;

namespace Favourites.Application.Tags.GetTagsDuplicates;

public sealed record TagDuplicateGroupDto(
    IReadOnlyList<TagDto> Tags);
