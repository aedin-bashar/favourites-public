namespace Favourites.Application.Tags.GetTagsSummary;

public sealed record TagsSummaryDto(
    int TotalTags,
    int UnusedTags,
    TagMostUsedDto? MostUsed,
    TagSummaryItemDto? RecentlyAdded,
    int PossibleDuplicates);

public sealed record TagMostUsedDto(Guid Id, string Name, int Count);

public sealed record TagSummaryItemDto(Guid Id, string Name);
