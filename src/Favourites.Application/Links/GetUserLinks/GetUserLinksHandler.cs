using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Favourites.Application.Categories.Mapping;
using Favourites.Application.Links.Dtos;
using Favourites.Application.Links.Mapping;
using Favourites.Application.Tags.Dtos;
using Favourites.Application.Tags.Mapping;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.GetUserLinks;

public sealed class GetUserLinksHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    private const int MaxPageSize = 100;

    public async Task<PagedLinksDto> HandleAsync(
        GetUserLinksQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var links = dbContext.FavouriteLinks
            .AsNoTracking()
            .Where(link => link.UserId == userId);

        links = query.Archived switch
        {
            ArchivedFilter.Archived => links.Where(link => link.IsArchived),
            ArchivedFilter.All => links,
            _ => links.Where(link => !link.IsArchived),
        };

        if (query.ArchivedFrom is { } archivedFrom)
        {
            links = links.Where(link => (link.UpdatedAtUtc ?? link.CreatedAtUtc) >= archivedFrom);
        }

        if (query.ArchivedTo is { } archivedTo)
        {
            links = links.Where(link => (link.UpdatedAtUtc ?? link.CreatedAtUtc) <= archivedTo);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim().ToLower();
            links = links.Where(link =>
                link.Title.ToLower().Contains(needle)
                || (link.Description != null && link.Description.ToLower().Contains(needle))
                || link.Url.ToLower().Contains(needle));
        }

        if (query.CategoryId is { } categoryFilterId && categoryFilterId != Guid.Empty)
        {
            var categoryBelongsToUser = await dbContext.Categories
                .AsNoTracking()
                .AnyAsync(
                    category => category.Id == categoryFilterId && category.UserId == userId,
                    cancellationToken);

            if (!categoryBelongsToUser)
                return new PagedLinksDto(Array.Empty<FavouriteLinkDto>(), 0, query.Page, query.PageSize);

            links = links.Where(link => link.CategoryId == categoryFilterId);
        }

        if (query.TagId is { } tagFilterId && tagFilterId != Guid.Empty)
        {
            var tagBelongsToUser = await dbContext.Tags
                .AsNoTracking()
                .AnyAsync(
                    tag => tag.Id == tagFilterId && tag.UserId == userId,
                    cancellationToken);

            if (!tagBelongsToUser)
                return new PagedLinksDto(Array.Empty<FavouriteLinkDto>(), 0, query.Page, query.PageSize);

            var taggedLinkIds = dbContext.FavouriteLinkTags
                .AsNoTracking()
                .Where(linkTag => linkTag.TagId == tagFilterId)
                .Select(linkTag => linkTag.FavouriteLinkId);

            links = links.Where(link => taggedLinkIds.Contains(link.Id));
        }

        var sorted = ApplySort(links, query.Sort);

        // Pagination: PageSize 0 means "return all" for backward-compatible callers.
        var pageSize = query.PageSize > 0 ? Math.Min(query.PageSize, MaxPageSize) : 0;
        int total;
        List<Favourites.Domain.Entities.FavouriteLink> ordered;

        if (pageSize > 0)
        {
            total = await sorted.CountAsync(cancellationToken);
            var page = Math.Max(1, query.Page);
            ordered = await sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }
        else
        {
            ordered = await sorted.ToListAsync(cancellationToken);
            total = ordered.Count;
        }

        if (ordered.Count == 0)
            return new PagedLinksDto(Array.Empty<FavouriteLinkDto>(), total, query.Page, pageSize > 0 ? pageSize : total);

        var linkIds = ordered.Select(link => link.Id).ToArray();

        var linkTags = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(linkTag => linkIds.Contains(linkTag.FavouriteLinkId))
            .ToListAsync(cancellationToken);

        var tagIds = linkTags.Select(lt => lt.TagId).Distinct().ToArray();

        Dictionary<Guid, TagDto> tagsById = tagIds.Length == 0
            ? new Dictionary<Guid, TagDto>()
            : await dbContext.Tags
                .AsNoTracking()
                .Where(tag => tag.UserId == userId && tagIds.Contains(tag.Id))
                .OrderBy(tag => tag.Name)
                .ToDictionaryAsync(tag => tag.Id, tag => tag.ToDto(), cancellationToken);

        var tagsByLinkId = linkTags
            .GroupBy(lt => lt.FavouriteLinkId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Select(lt => tagsById.TryGetValue(lt.TagId, out var t) ? t : null)
                    .Where(t => t is not null)
                    .Select(t => t!)
                    .OrderBy(t => t.Name)
                    .ToList());

        var categoryIds = ordered
            .Where(l => l.CategoryId is not null)
            .Select(l => l.CategoryId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, CategoryDto> categoriesById = categoryIds.Length == 0
            ? new Dictionary<Guid, CategoryDto>()
            : await dbContext.Categories
                .AsNoTracking()
                .Where(c => c.UserId == userId && categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.ToDto(), cancellationToken);

        var items = ordered
            .Select(link =>
                link.ToDto(
                    tagsByLinkId.TryGetValue(link.Id, out var tags) ? tags : Array.Empty<TagDto>(),
                    link.CategoryId is { } id && categoriesById.TryGetValue(id, out var cat) ? cat : null))
            .ToList();

        return new PagedLinksDto(items, total, Math.Max(1, query.Page), pageSize > 0 ? pageSize : total);
    }

    private static IQueryable<Favourites.Domain.Entities.FavouriteLink> ApplySort(
        IQueryable<Favourites.Domain.Entities.FavouriteLink> links,
        LinkSortOrder sort) => sort switch
        {
            LinkSortOrder.Oldest => links
                .OrderBy(l => l.CreatedAtUtc)
                .ThenBy(l => l.Id),
            LinkSortOrder.Title => links
                .OrderBy(l => l.Title)
                .ThenBy(l => l.Id),
            LinkSortOrder.RecentlyUpdated => links
                .OrderByDescending(l => l.UpdatedAtUtc ?? l.CreatedAtUtc)
                .ThenBy(l => l.Id),
            LinkSortOrder.OldestArchived => links
                .OrderBy(l => l.UpdatedAtUtc ?? l.CreatedAtUtc)
                .ThenBy(l => l.Id),
            _ => links
                .OrderByDescending(l => l.CreatedAtUtc)
                .ThenBy(l => l.Id),
        };
}
