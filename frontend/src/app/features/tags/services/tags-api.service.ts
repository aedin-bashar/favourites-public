import { Injectable, inject } from '@angular/core';
import { map, type Observable } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import type {
  CreateTagRequest,
  PagedTagsResponse,
  TagResponse,
  TagsSummaryResponse,
  UpdateTagRequest,
} from '../models/tag.models';

export type TagsSortOrder = 'name' | 'most-used' | 'least-used' | 'newest';
export type TagsStatusFilter = 'all' | 'used' | 'unused';

export interface TagsListFilters {
  readonly page?: number | null;
  readonly pageSize?: number | null;
  readonly search?: string | null;
  readonly status?: TagsStatusFilter | null;
  readonly sort?: TagsSortOrder | null;
}

@Injectable({ providedIn: 'root' })
export class TagsApiService {
  private readonly api = inject(ApiClient);

  /** Paginated tags list — used by the Tags page. */
  list(filtersOrPage: TagsListFilters | number = 1, pageSize = 25): Observable<PagedTagsResponse> {
    const filters =
      typeof filtersOrPage === 'number'
        ? { page: filtersOrPage, pageSize }
        : filtersOrPage;
    const params = buildParams(filters);
    const options = Object.keys(params).length > 0 ? { params } : undefined;
    return this.api.get<PagedTagsResponse>('/api/tags', options);
  }

  /** All tags (flat array) — used by pickers, dashboard, link-list. */
  listAll(): Observable<TagResponse[]> {
    // Request a large page so we get everything in one shot.
    return this.list(1, 100).pipe(map((paged) => paged.items));
  }

  summary(): Observable<TagsSummaryResponse> {
    return this.api.get<TagsSummaryResponse>('/api/tags/summary');
  }

  create(payload: CreateTagRequest): Observable<TagResponse> {
    return this.api.post<TagResponse, CreateTagRequest>('/api/tags', payload);
  }

  update(id: string, payload: UpdateTagRequest): Observable<TagResponse> {
    return this.api.put<TagResponse, UpdateTagRequest>(
      `/api/tags/${encodeURIComponent(id)}`,
      payload,
    );
  }

  remove(id: string): Observable<void> {
    return this.api.delete<void>(`/api/tags/${encodeURIComponent(id)}`);
  }

  /** Groups of tags with similar names (case-insensitive or Levenshtein ≤ 2). */
  duplicates(): Observable<TagDuplicateGroup[]> {
    return this.api.get<TagDuplicateGroup[]>('/api/tags/duplicates');
  }

  /** Merge tags into one. */
  merge(keepTagId: string, mergeTagIds: string[]): Observable<{ merged: number }> {
    return this.api.post<{ merged: number }, { keepTagId: string; mergeTagIds: string[] }>(
      '/api/tags/merge',
      { keepTagId, mergeTagIds },
    );
  }
}

export interface TagDuplicateGroup {
  readonly tags: import('../models/tag.models').TagResponse[];
}

function buildParams(filters?: TagsListFilters | null): Record<string, string> {
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
