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

namespace Favourites.Application.Links.GetArchivedSummary;

public sealed class GetArchivedSummaryHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    private const int CleanupThresholdDays = 90;
    private const int MaxCleanupSuggestions = 5;

    public async Task<ArchivedSummaryDto> HandleAsync(
        GetArchivedSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var restoredSince = now.AddDays(-30);
        var cleanupCutoff = now.AddDays(-CleanupThresholdDays);

        var archivedLinks = await dbContext.FavouriteLinks
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId && l.IsArchived, cancellationToken);

        var archivedThisMonth = await dbContext.FavouriteLinks
            .AsNoTracking()
            .CountAsync(
                l => l.UserId == userId && l.IsArchived && l.UpdatedAtUtc >= monthStart,
                cancellationToken);

        // Oldest archived: the link with the earliest UpdatedAtUtc (archive date)
        var oldestLinks = await dbContext.FavouriteLinks
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.IsArchived)
            .OrderBy(l => l.UpdatedAtUtc ?? l.CreatedAtUtc)
            .ThenBy(l => l.Id)
            .Take(1)
            .ToListAsync(cancellationToken);

        // Restored recently: active links whose UpdatedAtUtc is within the last 30 days
        // (proxy for a recent restore — IsArchived=false + recently updated)
        var restoredRecently = await dbContext.FavouriteLinks
            .AsNoTracking()
            .CountAsync(
                l => l.UserId == userId && !l.IsArchived && l.UpdatedAtUtc >= restoredSince,
                cancellationToken);

        // Cleanup suggestions: archived links older than 90 days, oldest first
        var suggestionLinks = await dbContext.FavouriteLinks
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.IsArchived && l.UpdatedAtUtc < cleanupCutoff)
            .OrderBy(l => l.UpdatedAtUtc ?? l.CreatedAtUtc)
            .ThenBy(l => l.Id)
            .Take(MaxCleanupSuggestions)
            .ToListAsync(cancellationToken);

        var linksToMap = oldestLinks
            .Concat(suggestionLinks)
            .GroupBy(link => link.Id)
            .Select(group => group.First())
            .ToList();

        var mappedLinks = await MapLinksAsync(linksToMap, userId, cancellationToken);
        var mappedById = mappedLinks.ToDictionary(link => link.Id);
        var oldest = oldestLinks.Count == 0 ? null : mappedById[oldestLinks[0].Id];
        var suggestions = suggestionLinks
            .Where(link => mappedById.ContainsKey(link.Id))
            .Select(link => mappedById[link.Id])
            .ToList();

        return new ArchivedSummaryDto(
            ArchivedLinks: archivedLinks,
            ArchivedThisMonth: archivedThisMonth,
            OldestArchived: oldest,
            RestoredRecently: restoredRecently,
            CleanupSuggestions: suggestions);
    }

    private async Task<List<FavouriteLinkDto>> MapLinksAsync(
        IReadOnlyList<FavouriteLink> links,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (links.Count == 0)
            return new List<FavouriteLinkDto>();

        var linkIds = links.Select(link => link.Id).ToArray();

        var linkTags = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(linkTag => linkIds.Contains(linkTag.FavouriteLinkId))
            .ToListAsync(cancellationToken);

        var tagIds = linkTags.Select(linkTag => linkTag.TagId).Distinct().ToArray();

        Dictionary<Guid, TagDto> tagsById = tagIds.Length == 0
            ? new Dictionary<Guid, TagDto>()
            : await dbContext.Tags
                .AsNoTracking()
                .Where(tag => tag.UserId == userId && tagIds.Contains(tag.Id))
                .OrderBy(tag => tag.Name)
                .ToDictionaryAsync(tag => tag.Id, tag => tag.ToDto(), cancellationToken);

        var tagsByLinkId = linkTags
            .GroupBy(linkTag => linkTag.FavouriteLinkId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(linkTag => tagsById.TryGetValue(linkTag.TagId, out var tag) ? tag : null)
                    .Where(tag => tag is not null)
                    .Select(tag => tag!)
                    .OrderBy(tag => tag.Name)
                    .ToList());

        var categoryIds = links
            .Where(link => link.CategoryId is not null)
            .Select(link => link.CategoryId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, CategoryDto> categoriesById = categoryIds.Length == 0
            ? new Dictionary<Guid, CategoryDto>()
            : await dbContext.Categories
                .AsNoTracking()
                .Where(category => category.UserId == userId && categoryIds.Contains(category.Id))
                .ToDictionaryAsync(category => category.Id, category => category.ToDto(), cancellationToken);

        return links
            .Select(link =>
                link.ToDto(
                    tagsByLinkId.TryGetValue(link.Id, out var tags) ? tags : Array.Empty<TagDto>(),
                    link.CategoryId is { } id && categoriesById.TryGetValue(id, out var category)
                        ? category
                        : null))
            .ToList();
    }
}
