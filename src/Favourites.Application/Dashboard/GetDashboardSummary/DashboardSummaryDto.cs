namespace Favourites.Application.Dashboard.GetDashboardSummary;

public sealed record DashboardSummaryDto(
    int TotalLinks,
    int TotalTags,
    int TotalCategories,
    int TotalArchived,
    DashboardThisWeekDto ThisWeek);

public sealed record DashboardThisWeekDto(
    int LinksAdded,
    int CategoriesCreated,
    int TagsCreated,
    int LinksArchived);
