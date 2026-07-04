using Favourites.Application.Categories.Dtos;

namespace Favourites.Application.Categories.GetCategoriesDuplicates;

public sealed record CategoryDuplicateGroupDto(
    IReadOnlyList<CategoryDto> Categories);
