namespace Favourites.Application.Links.GetUserLinks;

// Sort options exposed by `GET /api/links?sort=`. `Newest` is the
// default (matches the pre-3.11 behaviour); `RecentlyUpdated` orders by
// `UpdatedAtUtc` descending and falls back to `CreatedAtUtc` for links that
// have never been updated so the sort stays stable.
public enum LinkSortOrder
{
    Newest = 0,
    Oldest = 1,
    Title = 2,
    RecentlyUpdated = 3,
    OldestArchived = 4,
}
