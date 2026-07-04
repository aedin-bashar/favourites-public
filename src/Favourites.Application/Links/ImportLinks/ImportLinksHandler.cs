using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Domain.Entities;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.ImportLinks;

public sealed class ImportLinksHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    /// <summary>
    /// Marker comment written by the Favourites HTML export. Its presence tells
    /// the import that folders are categories (not tags) and that anchors carry
    /// a TAGS attribute, so a Favourites export round-trips with full fidelity.
    /// Browser exports lack the marker and keep the folder-becomes-tag behavior.
    /// </summary>
    public const string FavouritesExportMarker = "FAVOURITES-EXPORT";

    public async Task<ImportLinksResult> HandleAsync(
        ImportLinksCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

        var isFavouritesExport = command.HtmlContent.Contains(FavouritesExportMarker, StringComparison.Ordinal);

        var parsed = ParseBookmarks(command.HtmlContent);

        if (parsed.Count == 0)
            return new ImportLinksResult(Created: 0, Skipped: 0);

        var existingUrls = await dbContext.FavouriteLinks
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .Select(l => l.Url)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Folder names become categories for Favourites exports, tags for
        // browser exports. TAGS attributes (written by our export) always
        // become tags.
        var tagNames = parsed.SelectMany(b => b.TagNames);
        if (!isFavouritesExport)
            tagNames = tagNames.Concat(parsed.Select(b => b.FolderName).Where(f => f is not null).Select(f => f!));

        var tagNameMap = await EnsureTagsExistAsync(
            userId,
            tagNames.Distinct(StringComparer.OrdinalIgnoreCase),
            cancellationToken);

        var categoryNameMap = isFavouritesExport
            ? await EnsureCategoriesExistAsync(
                userId,
                parsed.Select(b => b.FolderName).Where(f => f is not null).Select(f => f!).Distinct(StringComparer.OrdinalIgnoreCase),
                cancellationToken)
            : new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);

        int created = 0;
        int skipped = 0;

        foreach (var bookmark in parsed)
        {
            if (existingUrls.Contains(bookmark.Url))
            {
                skipped++;
                continue;
            }

            Guid? categoryId = isFavouritesExport
                && bookmark.FolderName is not null
                && categoryNameMap.TryGetValue(bookmark.FolderName, out var category)
                ? category.Id
                : null;

            var link = FavouriteLink.Create(
                userId: userId,
                url: bookmark.Url,
                title: bookmark.Title,
                description: null,
                categoryId: categoryId,
                createdAtUtc: bookmark.AddDate);

            dbContext.FavouriteLinks.Add(link);

            var linkTagNames = isFavouritesExport
                ? bookmark.TagNames
                : bookmark.TagNames
                    .Concat(bookmark.FolderName is null ? [] : [bookmark.FolderName])
                    .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var tagName in linkTagNames)
            {
                if (tagNameMap.TryGetValue(tagName, out var tag))
                    dbContext.FavouriteLinkTags.Add(FavouriteLinkTag.Create(link.Id, tag.Id));
            }

            existingUrls.Add(bookmark.Url);
            created++;
        }

        if (created > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return new ImportLinksResult(Created: created, Skipped: skipped);
    }

    private static List<ParsedBookmark> ParseBookmarks(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var result = new List<ParsedBookmark>();

        var anchors = doc.DocumentNode.SelectNodes("//a");
        if (anchors is null)
            return result;

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttributeValue("href", string.Empty).Trim();
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            // Normalize the URL the same way FavouriteLink.Create does so that
            // the in-memory dedup set matches what the DB stores.
            var normalizedUrl = uri.ToString();

            var title = anchor.InnerText.Trim();
            if (string.IsNullOrEmpty(title))
                title = href;

            if (title.Length > FavouriteLink.MaxTitleLength)
                title = title[..FavouriteLink.MaxTitleLength];

            if (normalizedUrl.Length > FavouriteLink.MaxUrlLength)
                continue;

            var addDateAttr = anchor.GetAttributeValue("add_date", string.Empty);
            DateTimeOffset addDate = DateTimeOffset.UtcNow;
            if (long.TryParse(addDateAttr, out var unixSeconds) && unixSeconds > 0)
                addDate = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

            // The nearest preceding <H3> sibling-or-ancestor is the folder name.
            var folderName = GetNearestFolder(anchor);

            // Comma-separated TAGS attribute (written by the Favourites export;
            // also used by old Firefox exports).
            var tagNames = anchor.GetAttributeValue("tags", string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeTagName)
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.Add(new ParsedBookmark(normalizedUrl, title, addDate, folderName, tagNames));
        }

        return result;
    }

    private static string? GetNearestFolder(HtmlNode anchor)
    {
        // In the Netscape bookmark format a link belongs to a folder exactly
        // when its <A> sits inside the <DL> that follows the folder's
        // <DT><H3> heading. Browser exports close neither <DT> nor <p>, so
        // HtmlAgilityPack parses the folder's <DL> as a *child* of the folder
        // <DT> (and root-level links that follow a folder also end up inside
        // that <DT>). Folder membership therefore has to be resolved along
        // enclosing <DL> boundaries, not by sibling scanning from the anchor.
        for (var dl = ClosestAncestor(anchor.ParentNode, "dl");
             dl is not null;
             dl = ClosestAncestor(dl.ParentNode, "dl"))
        {
            var heading = FindOwningHeading(dl);
            if (heading is not null)
                return NormalizeTagName(heading.InnerText);
        }

        return null;
    }

    private static HtmlNode? ClosestAncestor(HtmlNode? node, string name)
    {
        while (node is not null && !node.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            node = node.ParentNode;
        return node;
    }

    private static HtmlNode? FindOwningHeading(HtmlNode dl)
    {
        // Case 1: unclosed <DT> markup (Chrome/Firefox/Safari/Edge exports) —
        // the <DL> is parsed as a descendant of the folder's <DT>, whose
        // first element child is the <H3>. Stop at the next <DL> so we never
        // borrow a heading from an outer folder.
        for (var node = dl.ParentNode;
             node is not null && !node.Name.Equals("dl", StringComparison.OrdinalIgnoreCase);
             node = node.ParentNode)
        {
            if (node.Name.Equals("dt", StringComparison.OrdinalIgnoreCase)
                && node.Element("h3") is { } nestedHeading)
            {
                return nestedHeading;
            }
        }

        // Case 2: well-formed markup — the folder's <DT><H3> (or a bare <H3>)
        // is the nearest preceding sibling of the <DL>.
        for (var sibling = dl.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling)
        {
            if (sibling.Name.Equals("h3", StringComparison.OrdinalIgnoreCase))
                return sibling;

            if (sibling.Name.Equals("dt", StringComparison.OrdinalIgnoreCase)
                && sibling.Element("h3") is { } siblingHeading)
            {
                return siblingHeading;
            }
        }

        return null;
    }

    private static string NormalizeTagName(string raw)
    {
        var name = HtmlEntity.DeEntitize(raw).Trim();
        if (name.Length > Tag.MaxNameLength)
            name = name[..Tag.MaxNameLength];
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

    private sealed record ParsedBookmark(
        string Url,
        string Title,
        DateTimeOffset AddDate,
        string? FolderName,
        IReadOnlyList<string> TagNames);
}
