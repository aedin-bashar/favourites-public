using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Favourites.Application.Categories.Mapping;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Categories.UpdateCategory;

public sealed class UpdateCategoryHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Returns null when the category does not exist OR belongs to another user.
    // The API maps null to 404 so cross-user updates are indistinguishable
    // from missing ids.
    public async Task<CategoryDto?> HandleAsync(
        UpdateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var category = await dbContext.Categories
            .SingleOrDefaultAsync(
                category => category.Id == command.Id && category.UserId == userId,
                cancellationToken);

        if (category is null)
        {
            return null;
        }

        category.UpdateName(command.Name);

        if (!string.IsNullOrWhiteSpace(command.Color))
        {
            category.UpdateColor(command.Color);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return category.ToDto();
    }
}
