namespace Favourites.Application.Links.GetUserLinks;

public sealed record GetUserLinksQuery(
    string? Search = null,
    Guid? TagId = null,
    Guid? CategoryId = null,
    LinkSortOrder Sort = LinkSortOrder.Newest,
    ArchivedFilter Archived = ArchivedFilter.Active,
    DateTimeOffset? ArchivedFrom = null,
    DateTimeOffset? ArchivedTo = null,
    int Page = 1,
    int PageSize = 0);   // 0 means "return all" — used by non-paginated callers
