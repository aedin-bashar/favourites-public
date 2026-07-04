using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Favourites.Application.Categories.Mapping;
using Favourites.Application.Links.Dtos;
using Favourites.Application.Links.Mapping;
using Favourites.Application.Tags.Mapping;
using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.GetFavouriteLinkById;

public sealed class GetFavouriteLinkByIdHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Returns null when the link does not exist OR belongs to another user.
    // The two cases are intentionally indistinguishable to the caller: leaking
    // "this id exists but isn't yours" would let an attacker enumerate other
    // users' link ids. The API layer maps null → 404.
    public async Task<FavouriteLinkDto?> HandleAsync(
        GetFavouriteLinkByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var link = await dbContext.FavouriteLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                link => link.Id == query.Id && link.UserId == userId,
                cancellationToken);

        if (link is null)
        {
            return null;
        }

        var tagIds = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(linkTag => linkTag.FavouriteLinkId == link.Id)
            .Select(linkTag => linkTag.TagId)
            .ToListAsync(cancellationToken);

        IReadOnlyList<Tag> tags = tagIds.Count == 0
            ? Array.Empty<Tag>()
            : await dbContext.Tags
                .AsNoTracking()
                .Where(tag => tag.UserId == userId && tagIds.Contains(tag.Id))
                .OrderBy(tag => tag.Name)
                .ToListAsync(cancellationToken);

        CategoryDto? categoryDto = null;
        if (link.CategoryId is { } categoryId)
        {
            var category = await dbContext.Categories
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    c => c.Id == categoryId && c.UserId == userId,
                    cancellationToken);

            categoryDto = category?.ToDto();
        }

        return link.ToDto(tags.Select(tag => tag.ToDto()).ToList(), categoryDto);
    }
}
