namespace Favourites.Api.Contracts.Categories;

public sealed record CreateCategoryRequest(string Name);

public sealed record UpdateCategoryRequest(string Name, string Color = "");

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Color = "#6c757d",
    int LinkCount = 0,
    DateTimeOffset CreatedAtUtc = default,
    DateTimeOffset? LastActivityAtUtc = null);

public sealed record PagedCategoriesResponse(
    IReadOnlyList<CategoryResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record CategoriesSummaryResponse(
    int TotalCategories,
    int EmptyCategories,
    CategoryLargestResponse? LargestCategory,
    CategorySummaryItemResponse? RecentlyAdded,
    int UncategorizedLinks = 0);

public sealed record CategoryLargestResponse(Guid Id, string Name, int Count);

public sealed record CategorySummaryItemResponse(Guid Id, string Name);
