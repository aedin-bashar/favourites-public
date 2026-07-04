using Favourites.Application.Links.Dtos;

namespace Favourites.Application.Links.GetArchivedSummary;

public sealed record ArchivedSummaryDto(
    int ArchivedLinks,
    int ArchivedThisMonth,
    FavouriteLinkDto? OldestArchived,
    int RestoredRecently,
    IReadOnlyList<FavouriteLinkDto> CleanupSuggestions);
