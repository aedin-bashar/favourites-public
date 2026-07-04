using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Favourites.Application.Categories.Mapping;
using Favourites.Application.Links.Dtos;
using Favourites.Application.Links.Mapping;
using Favourites.Application.Tags.Mapping;
using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.UpdateFavouriteLink;

public sealed class UpdateFavouriteLinkHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    // Returns null when the link does not exist OR belongs to another user.
    // Same ownership-not-found rule as GetFavouriteLinkByIdHandler — the API
    // maps null to 404 so cross-user updates can't be distinguished from
    // missing ids.
    public async Task<FavouriteLinkDto?> HandleAsync(
        UpdateFavouriteLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var link = await dbContext.FavouriteLinks
            .SingleOrDefaultAsync(
                link => link.Id == command.Id && link.UserId == userId,
                cancellationToken);

        if (link is null)
        {
            return null;
        }

        IReadOnlyList<Tag> tags;

        if (command.TagIds is null)
        {
            var existingTagIds = await dbContext.FavouriteLinkTags
                .AsNoTracking()
                .Where(linkTag => linkTag.FavouriteLinkId == link.Id)
                .Select(linkTag => linkTag.TagId)
                .ToListAsync(cancellationToken);

            tags = existingTagIds.Count == 0
                ? Array.Empty<Tag>()
                : await dbContext.Tags
                    .AsNoTracking()
                    .Where(tag => tag.UserId == userId && existingTagIds.Contains(tag.Id))
                    .OrderBy(tag => tag.Name)
                    .ToListAsync(cancellationToken);
        }
        else
        {
            var tagIds = command.TagIds
                .Distinct()
                .ToArray();

            var existingLinks = await dbContext.FavouriteLinkTags
                .Where(linkTag => linkTag.FavouriteLinkId == link.Id)
                .ToListAsync(cancellationToken);

            dbContext.FavouriteLinkTags.RemoveRange(existingLinks);

            foreach (var tagId in tagIds)
            {
                dbContext.FavouriteLinkTags.Add(FavouriteLinkTag.Create(link.Id, tagId));
            }

            tags = tagIds.Length == 0
                ? Array.Empty<Tag>()
                : await dbContext.Tags
                    .AsNoTracking()
                    .Where(tag => tag.UserId == userId && tagIds.Contains(tag.Id))
                    .OrderBy(tag => tag.Name)
                    .ToListAsync(cancellationToken);
        }

        link.UpdateContent(command.Url, command.Title, command.Description, command.CategoryId);

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

        await dbContext.SaveChangesAsync(cancellationToken);

        return link.ToDto(tags.Select(tag => tag.ToDto()).ToList(), categoryDto);
    }
}
