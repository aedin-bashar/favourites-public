using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Tags.Dtos;
using Favourites.Application.Tags.Mapping;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Tags.UpdateTag;

public sealed class UpdateTagHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Returns null when the tag does not exist OR belongs to another user.
    // The API maps null to 404 so cross-user updates are indistinguishable
    // from missing ids.
    public async Task<TagDto?> HandleAsync(
        UpdateTagCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var tag = await dbContext.Tags
            .SingleOrDefaultAsync(
                tag => tag.Id == command.Id && tag.UserId == userId,
                cancellationToken);

        if (tag is null)
        {
            return null;
        }

        tag.UpdateName(command.Name);

        await dbContext.SaveChangesAsync(cancellationToken);

        return tag.ToDto();
    }
}
