using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Favourites.Application.Categories.Mapping;
using Favourites.Application.Links.Dtos;
using Favourites.Application.Links.Mapping;
using Favourites.Application.Tags.Dtos;
using Favourites.Application.Tags.Mapping;
using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.GetCleanupSuggestions;

public sealed class GetCleanupSuggestionsHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    private const int CleanupThresholdDays = 90;

    public async Task<IReadOnlyList<FavouriteLinkDto>> HandleAsync(
        GetCleanupSuggestionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-CleanupThresholdDays);

        var links = await dbContext.FavouriteLinks
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.IsArchived
                        && (l.UpdatedAtUtc == null || l.UpdatedAtUtc < cutoff))
            .OrderBy(l => l.UpdatedAtUtc ?? l.CreatedAtUtc)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
            return Array.Empty<FavouriteLinkDto>();

        return await MapLinksAsync(links, userId, cancellationToken);
    }

    private async Task<List<FavouriteLinkDto>> MapLinksAsync(
        IReadOnlyList<FavouriteLink> links,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var linkIds = links.Select(l => l.Id).ToArray();

        var linkTags = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(lt => linkIds.Contains(lt.FavouriteLinkId))
            .ToListAsync(cancellationToken);

        var tagIds = linkTags.Select(lt => lt.TagId).Distinct().ToArray();

        Dictionary<Guid, TagDto> tagsById = tagIds.Length == 0
            ? []
            : await dbContext.Tags
                .AsNoTracking()
                .Where(t => t.UserId == userId && tagIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.ToDto(), cancellationToken);

        var tagsByLinkId = linkTags
            .GroupBy(lt => lt.FavouriteLinkId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Select(lt => tagsById.TryGetValue(lt.TagId, out var tag) ? tag : null)
                    .Where(t => t is not null)
                    .Select(t => t!)
                    .OrderBy(t => t.Name)
                    .ToList());

        var categoryIds = links
            .Where(l => l.CategoryId is not null)
            .Select(l => l.CategoryId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, CategoryDto> categoriesById = categoryIds.Length == 0
            ? []
            : await dbContext.Categories
                .AsNoTracking()
                .Where(c => c.UserId == userId && categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.ToDto(), cancellationToken);

        return links
            .Select(link =>
                link.ToDto(
                    tagsByLinkId.TryGetValue(link.Id, out var tags) ? tags : Array.Empty<TagDto>(),
                    link.CategoryId is { } id && categoriesById.TryGetValue(id, out var cat) ? cat : null))
            .ToList();
    }
}
