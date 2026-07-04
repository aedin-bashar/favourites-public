namespace Favourites.Api.Contracts.Tags;

public sealed record CreateTagRequest(string Name);

public sealed record UpdateTagRequest(string Name);

public sealed record TagResponse(
    Guid Id,
    string Name,
    int LinkCount = 0,
    DateTimeOffset CreatedAtUtc = default,
    DateTimeOffset? LastUsedAtUtc = null);

public sealed record PagedTagsResponse(
    IReadOnlyList<TagResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record TagsSummaryResponse(
    int TotalTags,
    int UnusedTags,
    TagMostUsedResponse? MostUsed,
    TagSummaryItemResponse? RecentlyAdded,
    int PossibleDuplicates);

public sealed record TagMostUsedResponse(Guid Id, string Name, int Count);

public sealed record TagSummaryItemResponse(Guid Id, string Name);
