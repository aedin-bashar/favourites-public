namespace Favourites.Application.Links.GetUserLinks;

// Archive scope exposed by `GET /api/links?archived=`.
// `Active` (default) hides archived links so the main lists keep working
// unchanged. `Archived` returns only archived links. `All` includes both —
// used by power features (e.g. an "Everything" view) and tests.
public enum ArchivedFilter
{
    Active = 0,
    Archived = 1,
    All = 2,
}
