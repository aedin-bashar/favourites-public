namespace Favourites.Api.Contracts.Links;

public sealed record ArchivedSummaryResponse(
    int ArchivedLinks,
    int ArchivedThisMonth,
    LinkResponse? OldestArchived,
    int RestoredRecently,
    IReadOnlyList<LinkResponse> CleanupSuggestions);

public sealed record RestoreManyLinksRequest(IReadOnlyList<Guid> LinkIds);
