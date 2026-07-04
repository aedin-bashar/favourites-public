// `features/categories/` — category management pages and API integration.

export type {
  CreateCategoryRequest,
  CategoryResponse,
  UpdateCategoryRequest,
} from './models';
export { CategoriesApiService } from './services';
export { CategoriesPageComponent } from './categories-page/categories-page.component';
