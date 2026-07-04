using System.Text.Json;
using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.ImportLinks;

public sealed class ImportJsonLinksHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ImportLinksResult> HandleAsync(
        ImportJsonLinksCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        // A malformed document throws JsonException — the controller translates
        // that into a 400 so the client sees a validation error, not a 500.
        var items = JsonSerializer.Deserialize<List<ExportedLink>>(command.JsonContent, SerializerOptions)
            ?? [];

        var parsed = Normalize(items);

        if (parsed.Count == 0)
            return new ImportLinksResult(Created: 0, Skipped: 0);

        var existingUrls = await dbContext.FavouriteLinks
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => l.Url)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var tagNameMap = await EnsureTagsExistAsync(
            userId,
            parsed.SelectMany(l => l.TagNames).Distinct(StringComparer.OrdinalIgnoreCase),
            cancellationToken);

        var categoryNameMap = await EnsureCategoriesExistAsync(
            userId,
            parsed.Select(l => l.CategoryName).Where(c => c is not null).Select(c => c!).Distinct(StringComparer.OrdinalIgnoreCase),
            cancellationToken);

        int created = 0;
        int skipped = 0;

        foreach (var item in parsed)
        {
            if (existingUrls.Contains(item.Url))
            {
                skipped++;
                continue;
            }

            Guid? categoryId = item.CategoryName is not null
                && categoryNameMap.TryGetValue(item.CategoryName, out var category)
                ? category.Id
                : null;

            var link = FavouriteLink.Create(
                userId: userId,
                url: item.Url,
                title: item.Title,
                description: item.Description,
                categoryId: categoryId,
                createdAtUtc: item.CreatedAtUtc);

            if (item.IsArchived)
                link.Archive();

            dbContext.FavouriteLinks.Add(link);

            foreach (var tagName in item.TagNames)
            {
                if (tagNameMap.TryGetValue(tagName, out var tag))
                    dbContext.FavouriteLinkTags.Add(FavouriteLinkTag.Create(link.Id, tag.Id));
            }

            existingUrls.Add(item.Url);
            created++;
        }

        if (created > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return new ImportLinksResult(Created: created, Skipped: skipped);
    }

    private static List<ParsedLink> Normalize(List<ExportedLink> items)
    {
        var result = new List<ParsedLink>();

        foreach (var item in items)
        {
            var href = item.Url?.Trim() ?? string.Empty;
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            // Normalize the URL the same way FavouriteLink.Create does so that
            // the in-memory dedup set matches what the DB stores.
            var normalizedUrl = uri.ToString();
            if (normalizedUrl.Length > FavouriteLink.MaxUrlLength)
                continue;

            var title = item.Title?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(title))
                title = normalizedUrl;
            if (title.Length > FavouriteLink.MaxTitleLength)
                title = title[..FavouriteLink.MaxTitleLength];

            var description = item.Description?.Trim();
            if (string.IsNullOrEmpty(description))
                description = null;
            else if (description.Length > FavouriteLink.MaxDescriptionLength)
                description = description[..FavouriteLink.MaxDescriptionLength];

            var tagNames = (item.Tags ?? [])
                .Select(t => NormalizeName(t.Name, Tag.MaxNameLength))
                .Where(n => n is not null)
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categoryName = NormalizeName(item.Category?.Name, Category.MaxNameLength);

            result.Add(new ParsedLink(
                Url: normalizedUrl,
                Title: title,
                Description: description,
                IsArchived: item.IsArchived,
                CreatedAtUtc: item.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                TagNames: tagNames,
                CategoryName: categoryName));
        }

        return result;
    }

    private static string? NormalizeName(string? raw, int maxLength)
    {
        var name = raw?.Trim();
        if (string.IsNullOrEmpty(name))
            return null;
        if (name.Length > maxLength)
            name = name[..maxLength];
        return name;
    }

    private async Task<Dictionary<string, Tag>> EnsureTagsExistAsync(
        Guid userId,
        IEnumerable<string> tagNames,
        CancellationToken cancellationToken)
    {
        var names = tagNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
            return new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

        var existing = await dbContext.Tags
            .Where(t => t.UserId == userId && names.Contains(t.Name))
            .ToListAsync(cancellationToken);

        var map = existing.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (map.ContainsKey(name))
                continue;

            var tag = Tag.Create(userId, name);
            dbContext.Tags.Add(tag);
            map[name] = tag;
        }

        return map;
    }

    private async Task<Dictionary<string, Category>> EnsureCategoriesExistAsync(
        Guid userId,
        IEnumerable<string> categoryNames,
        CancellationToken cancellationToken)
    {
        var names = categoryNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
            return new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);

        var existing = await dbContext.Categories
            .Where(c => c.UserId == userId && names.Contains(c.Name))
            .ToListAsync(cancellationToken);

        var map = existing.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (map.ContainsKey(name))
                continue;

            var category = Category.Create(userId, name);
            dbContext.Categories.Add(category);
            map[name] = category;
        }

        return map;
    }

    // Mirrors the camelCase shape produced by GET /api/links/export?format=json.
    // Unknown properties (id, updatedAtUtc, tag/category ids and colors) are ignored;
    // tags and categories are re-linked by name so imports work across accounts.
    private sealed record ExportedLink(
        string? Url,
        string? Title,
        string? Description,
        bool IsArchived,
        DateTimeOffset? CreatedAtUtc,
        List<ExportedTag>? Tags,
        ExportedCategory? Category);

    private sealed record ExportedTag(string? Name);

    private sealed record ExportedCategory(string? Name);

    private sealed record ParsedLink(
        string Url,
        string Title,
        string? Description,
        bool IsArchived,
        DateTimeOffset CreatedAtUtc,
        List<string> TagNames,
        string? CategoryName);
}
