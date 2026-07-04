using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Tags.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Tags.GetUserTags;

public sealed class GetUserTagsHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<IReadOnlyList<TagDto>> HandleAsync(
        GetUserTagsQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var tags = await dbContext.Tags
            .AsNoTracking()
            .Where(tag => tag.UserId == userId)
            .OrderBy(tag => tag.Name)
            .ToListAsync(cancellationToken);

        var tagIds = tags.Select(tag => tag.Id).ToArray();

        var usage = tagIds.Length == 0
            ? new Dictionary<Guid, TagUsage>()
            : await dbContext.FavouriteLinkTags
                .AsNoTracking()
                .Where(linkTag => tagIds.Contains(linkTag.TagId))
                .Join(
                    dbContext.FavouriteLinks.AsNoTracking().Where(link => link.UserId == userId),
                    linkTag => linkTag.FavouriteLinkId,
                    link => link.Id,
                    (linkTag, link) => new
                    {
                        linkTag.TagId,
                        ActivityAtUtc = link.UpdatedAtUtc ?? link.CreatedAtUtc,
                    })
                .GroupBy(item => item.TagId)
                .Select(group => new
                {
                    TagId = group.Key,
                    Count = group.Count(),
                    LastUsedAtUtc = group.Max(item => item.ActivityAtUtc),
                })
                .ToDictionaryAsync(
                    item => item.TagId,
                    item => new TagUsage(item.Count, item.LastUsedAtUtc),
                    cancellationToken);

        return tags
            .Select(tag =>
            {
                usage.TryGetValue(tag.Id, out var tagUsage);
                return new TagDto(
                    Id: tag.Id,
                    Name: tag.Name,
                    LinkCount: tagUsage?.Count ?? 0,
                    CreatedAtUtc: tag.CreatedAtUtc,
                    LastUsedAtUtc: tagUsage?.LastUsedAtUtc);
            })
            .ToList();
    }

    private sealed record TagUsage(int Count, DateTimeOffset LastUsedAtUtc);
}
