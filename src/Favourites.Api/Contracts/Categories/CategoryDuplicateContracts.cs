namespace Favourites.Api.Contracts.Categories;

public sealed record CategoryDuplicateGroupResponse(IReadOnlyList<CategoryResponse> Categories);

public sealed record MergeCategoriesRequest(Guid KeepCategoryId, IReadOnlyList<Guid> MergeCategoryIds);
