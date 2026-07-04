using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Tags.DeleteTag;

public sealed class DeleteTagHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Returns false for missing OR cross-user ids, matching the same
    // ownership-not-found rule used by links.
    public async Task<bool> HandleAsync(
        DeleteTagCommand command,
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
            return false;
        }

        var linkTags = await dbContext.FavouriteLinkTags
            .Where(linkTag => linkTag.TagId == tag.Id)
            .ToListAsync(cancellationToken);

        dbContext.FavouriteLinkTags.RemoveRange(linkTags);
        dbContext.Tags.Remove(tag);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
