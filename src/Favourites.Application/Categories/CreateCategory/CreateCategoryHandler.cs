using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Favourites.Application.Categories.Mapping;
using Favourites.Domain.Entities;

namespace Favourites.Application.Categories.CreateCategory;

public sealed class CreateCategoryHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<CategoryDto> HandleAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var category = Category.Create(userId, command.Name);

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return category.ToDto();
    }
}
