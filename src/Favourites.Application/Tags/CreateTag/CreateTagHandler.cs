using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Tags.Dtos;
using Favourites.Application.Tags.Mapping;
using Favourites.Domain.Entities;

namespace Favourites.Application.Tags.CreateTag;

public sealed class CreateTagHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<TagDto> HandleAsync(
        CreateTagCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var tag = Tag.Create(userId, command.Name);

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tag.ToDto();
    }
}
