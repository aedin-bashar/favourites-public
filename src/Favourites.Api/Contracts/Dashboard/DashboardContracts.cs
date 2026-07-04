namespace Favourites.Api.Contracts.Dashboard;

public sealed record DashboardSummaryResponse(
    int TotalLinks,
    int TotalTags,
    int TotalCategories,
    int TotalArchived,
    DashboardThisWeekResponse ThisWeek);

public sealed record DashboardThisWeekResponse(
    int LinksAdded,
    int CategoriesCreated,
    int TagsCreated,
    int LinksArchived);
