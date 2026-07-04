using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Categories.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Categories.GetCategoriesDuplicates;

public sealed class GetCategoriesDuplicatesHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    private const int LevenshteinThreshold = 2;

    public async Task<IReadOnlyList<CategoryDuplicateGroupDto>> HandleAsync(
        GetCategoriesDuplicatesQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (categories.Count < 2)
            return Array.Empty<CategoryDuplicateGroupDto>();

        var linkCounts = await dbContext.FavouriteLinks
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.CategoryId != null)
            .GroupBy(l => l.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

        var catDtos = categories.Select(c =>
        {
            linkCounts.TryGetValue(c.Id, out var count);
            return new CategoryDto(c.Id, c.Name, c.Color, count, c.CreatedAtUtc);
        }).ToList();

        var groups = new List<CategoryDuplicateGroupDto>();
        var assigned = new HashSet<Guid>();

        for (int i = 0; i < catDtos.Count; i++)
        {
            if (assigned.Contains(catDtos[i].Id))
                continue;

            var group = new List<CategoryDto> { catDtos[i] };

            for (int j = i + 1; j < catDtos.Count; j++)
            {
                if (assigned.Contains(catDtos[j].Id))
                    continue;

                if (AreDuplicates(catDtos[i].Name, catDtos[j].Name))
                {
                    group.Add(catDtos[j]);
                    assigned.Add(catDtos[j].Id);
                }
            }

            if (group.Count > 1)
            {
                assigned.Add(catDtos[i].Id);
                groups.Add(new CategoryDuplicateGroupDto(group));
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
