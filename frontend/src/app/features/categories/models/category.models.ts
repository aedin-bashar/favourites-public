// Frontend mirrors of the backend category contracts.
// Source of truth: src/Favourites.Api/Contracts/Categories/CategoryContracts.cs
// JSON over the wire is camelCase (ASP.NET Core default).

/**
 * Body sent to `POST /api/categories` to create a new category.
 *
 * `UserId` is intentionally absent. The server resolves ownership from
 * the authenticated cookie and ignores any client-supplied owner.
 */
export interface CreateCategoryRequest {
  readonly name: string;
}

/**
 * Body sent to `PUT /api/categories/{id}` to rename an existing category.
 */
export interface UpdateCategoryRequest {
  readonly name: string;
  readonly color: string;
}

/**
 * Shape of a category as returned by the API.
 */
export interface CategoryResponse {
  readonly id: string;
  readonly name: string;
  readonly color: string;
  readonly linkCount: number;
  readonly createdAtUtc: string;
  readonly lastActivityAtUtc: string | null;
}

/**
 * Paginated wrapper returned by `GET /api/categories?page=N&pageSize=N`.
 * Mirrors {@link PagedCategoriesResponse} from the backend contracts.
 */
export interface PagedCategoriesResponse {
  readonly items: CategoryResponse[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

/**
 * Summary data returned by `GET /api/categories/summary`.
 * Mirrors {@link CategoriesSummaryResponse} from the backend contracts.
 */
export interface CategoriesSummaryResponse {
  readonly totalCategories: number;
  readonly emptyCategories: number;
  readonly largestCategory: CategoryLargestResponse | null;
  readonly recentlyAdded: CategorySummaryItemResponse | null;
  readonly uncategorizedLinks: number;
}

export interface CategoryLargestResponse {
  readonly id: string;
  readonly name: string;
  readonly count: number;
}

export interface CategorySummaryItemResponse {
  readonly id: string;
  readonly name: string;
}
