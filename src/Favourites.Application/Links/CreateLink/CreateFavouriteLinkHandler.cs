using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Favourites.Application.Categories.Mapping;
using Favourites.Application.Links.Dtos;
using Favourites.Application.Links.Mapping;
using Favourites.Application.Tags.Mapping;
using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.CreateLink;

public sealed class CreateFavouriteLinkHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<FavouriteLinkDto> HandleAsync(
        CreateFavouriteLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var tagIds = command.TagIds?
            .Distinct()
            .ToArray() ?? [];

        var link = FavouriteLink.Create(
            userId: userId,
            url: command.Url,
            title: command.Title,
            description: command.Description,
            categoryId: command.CategoryId);

        dbContext.FavouriteLinks.Add(link);

        foreach (var tagId in tagIds)
        {
            dbContext.FavouriteLinkTags.Add(FavouriteLinkTag.Create(link.Id, tagId));
        }

        IReadOnlyList<Tag> tags = tagIds.Length == 0
            ? Array.Empty<Tag>()
            : await dbContext.Tags
                .AsNoTracking()
                .Where(tag => tag.UserId == userId && tagIds.Contains(tag.Id))
                .OrderBy(tag => tag.Name)
                .ToListAsync(cancellationToken);

        CategoryDto? categoryDto = null;
        if (command.CategoryId is { } categoryId)
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
