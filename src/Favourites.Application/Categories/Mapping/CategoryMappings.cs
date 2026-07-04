using Favourites.Application.Categories.Dtos;
using Favourites.Domain.Entities;

namespace Favourites.Application.Categories.Mapping;

public static class CategoryMappings
{
    public static CategoryDto ToDto(this Category category) => new(
        Id: category.Id,
        Name: category.Name,
        Color: category.Color,
        CreatedAtUtc: category.CreatedAtUtc);
}
