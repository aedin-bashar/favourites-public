import { Injectable, inject } from '@angular/core';
import { map, type Observable } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import type {
  CategoriesSummaryResponse,
  CategoryResponse,
  CreateCategoryRequest,
  PagedCategoriesResponse,
  UpdateCategoryRequest,
} from '../models/category.models';

export type CategoriesSortOrder = 'name' | 'largest' | 'newest' | 'recently-active';
export type CategoriesStatusFilter = 'all' | 'used' | 'empty';

export interface CategoriesListFilters {
  readonly page?: number | null;
  readonly pageSize?: number | null;
  readonly search?: string | null;
  readonly status?: CategoriesStatusFilter | null;
  readonly sort?: CategoriesSortOrder | null;
}

/**
 * HTTP client for the `/api/categories` endpoints.
 *
 * State management and UI feedback belong in the page component that consumes
 * this service; this layer stays a small typed wrapper around the backend
 * contracts and mirrors {@link import('../../tags/services/tags-api.service').TagsApiService}
 * to keep the two domains symmetrical.
 */
@Injectable({ providedIn: 'root' })
export class CategoriesApiService {
  private readonly api = inject(ApiClient);

  /** Paginated categories list — used by the Categories page. */
  list(
    filtersOrPage: CategoriesListFilters | number = 1,
    pageSize = 25,
  ): Observable<PagedCategoriesResponse> {
    const filters =
      typeof filtersOrPage === 'number'
        ? { page: filtersOrPage, pageSize }
        : filtersOrPage;
    const params = buildParams(filters);
    const options = Object.keys(params).length > 0 ? { params } : undefined;
    return this.api.get<PagedCategoriesResponse>('/api/categories', options);
  }

  /** All categories (flat array) — used by pickers, dashboard, link-list. */
  listAll(): Observable<CategoryResponse[]> {
    // Request a large page so we get everything in one shot.
    return this.list(1, 100).pipe(map((paged) => paged.items));
  }

  summary(): Observable<CategoriesSummaryResponse> {
    return this.api.get<CategoriesSummaryResponse>('/api/categories/summary');
  }

  /**
   * Creates a category for the authenticated user.
   * Backend route: `POST /api/categories`.
   */
  create(payload: CreateCategoryRequest): Observable<CategoryResponse> {
    return this.api.post<CategoryResponse, CreateCategoryRequest>(
      '/api/categories',
      payload,
    );
  }

  /**
   * Renames an owned category.
   * Backend route: `PUT /api/categories/{id}`.
   * Missing and cross-user ids both surface as 404.
   */
  update(id: string, payload: UpdateCategoryRequest): Observable<CategoryResponse> {
    return this.api.put<CategoryResponse, UpdateCategoryRequest>(
      `/api/categories/${encodeURIComponent(id)}`,
      payload,
    );
  }

  /**
   * Deletes an owned category.
   * Backend route: `DELETE /api/categories/{id}`.
   * Resolves with no value on 204 No Content.
   */
  remove(id: string): Observable<void> {
    return this.api.delete<void>(`/api/categories/${encodeURIComponent(id)}`);
  }

  /** Groups of categories with similar names. */
  duplicates(): Observable<CategoryDuplicateGroup[]> {
    return this.api.get<CategoryDuplicateGroup[]>('/api/categories/duplicates');
  }

  /** Merge categories into one. */
  merge(keepCategoryId: string, mergeCategoryIds: string[]): Observable<{ merged: number }> {
    return this.api.post<{ merged: number }, { keepCategoryId: string; mergeCategoryIds: string[] }>(
      '/api/categories/merge',
      { keepCategoryId, mergeCategoryIds },
    );
  }
}

export interface CategoryDuplicateGroup {
  readonly categories: import('../models/category.models').CategoryResponse[];
}

function buildParams(filters?: CategoriesListFilters | null): Record<string, string> {
  const params: Record<string, string> = {};
  const page = filters?.page ?? 1;
  const pageSize = filters?.pageSize ?? 25;
  params['page'] = String(page > 0 ? page : 1);
  params['pageSize'] = String(pageSize > 0 ? pageSize : 25);
  const search = filters?.search?.trim();
  if (search) params['search'] = search;
  if (filters?.status && filters.status !== 'all') params['status'] = filters.status;
  if (filters?.sort) params['sort'] = filters.sort;
  return params;
}
