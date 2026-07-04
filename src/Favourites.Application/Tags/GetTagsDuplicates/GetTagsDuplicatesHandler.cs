using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Tags.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Tags.GetTagsDuplicates;

public sealed class GetTagsDuplicatesHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    private const int LevenshteinThreshold = 2;

    public async Task<IReadOnlyList<TagDuplicateGroupDto>> HandleAsync(
        GetTagsDuplicatesQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var tags = await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        if (tags.Count < 2)
            return Array.Empty<TagDuplicateGroupDto>();

        var linkCounts = await dbContext.FavouriteLinkTags
            .AsNoTracking()
            .Where(lt => dbContext.Tags
                .Where(t => t.UserId == userId)
                .Select(t => t.Id)
                .Contains(lt.TagId))
            .GroupBy(lt => lt.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TagId, x => x.Count, cancellationToken);

        var tagDtos = tags.Select(t =>
        {
            linkCounts.TryGetValue(t.Id, out var count);
            return new TagDto(t.Id, t.Name, count, t.CreatedAtUtc);
        }).ToList();

        var groups = new List<TagDuplicateGroupDto>();
        var assigned = new HashSet<Guid>();

        for (int i = 0; i < tagDtos.Count; i++)
        {
            if (assigned.Contains(tagDtos[i].Id))
                continue;

            var group = new List<TagDto> { tagDtos[i] };

            for (int j = i + 1; j < tagDtos.Count; j++)
            {
                if (assigned.Contains(tagDtos[j].Id))
                    continue;

                if (AreDuplicates(tagDtos[i].Name, tagDtos[j].Name))
                {
                    group.Add(tagDtos[j]);
                    assigned.Add(tagDtos[j].Id);
                }
            }

            if (group.Count > 1)
            {
                assigned.Add(tagDtos[i].Id);
                groups.Add(new TagDuplicateGroupDto(group));
            }
        }

        return groups;
    }

    private static bool AreDuplicates(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return true;

        return Levenshtein(
            a.ToLowerInvariant(),
            b.ToLowerInvariant()) <= LevenshteinThreshold;
    }

    private static int Levenshtein(string s, string t)
    {
        if (s.Length == 0) return t.Length;
        if (t.Length == 0) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }
}
