namespace Favourites.Application.Categories.MergeCategories;

/// <summary>
/// Merges one or more categories into a single target category.
/// Links from the merged categories are reassigned to <see cref="KeepCategoryId"/>,
/// and the merged categories are deleted.
/// </summary>
public sealed record MergeCategoriesCommand(
    Guid KeepCategoryId,
    IReadOnlyList<Guid> MergeCategoryIds);
