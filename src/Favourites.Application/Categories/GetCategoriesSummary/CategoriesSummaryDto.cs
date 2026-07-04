namespace Favourites.Application.Categories.GetCategoriesSummary;

public sealed record CategoriesSummaryDto(
    int TotalCategories,
    int EmptyCategories,
    CategoryLargestDto? LargestCategory,
    CategorySummaryItemDto? RecentlyAdded,
    int UncategorizedLinks);

public sealed record CategoryLargestDto(Guid Id, string Name, int Count);

public sealed record CategorySummaryItemDto(Guid Id, string Name);
