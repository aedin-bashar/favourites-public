import { Injectable, inject } from '@angular/core';
import { map, type Observable } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import type {
  CreateLinkRequest,
  LinkResponse,
  UpdateLinkRequest,
} from '../models/link.models';

export type LinksSortOrder =
  | 'newest'
  | 'oldest'
  | 'title'
  | 'recently-updated'
  | 'oldest-archived';
export type LinksArchivedFilter = 'active' | 'archived' | 'all';

export interface ListLinksFilters {
  readonly search?: string | null;
  readonly tagId?: string | null;
  readonly categoryId?: string | null;
  readonly sort?: LinksSortOrder | null;
  readonly archived?: LinksArchivedFilter | null;
  readonly archivedFrom?: string | null;
  readonly archivedTo?: string | null;
}

export interface PagedLinksFilters extends ListLinksFilters {
  readonly page?: number | null;
  readonly pageSize?: number | null;
}

/** Paginated response envelope from `GET /api/links?page=&pageSize=`. */
export interface PagedLinksResponse {
  readonly items: LinkResponse[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class LinksApiService {
  private readonly api = inject(ApiClient);

  create(payload: CreateLinkRequest): Observable<LinkResponse> {
    return this.api.post<LinkResponse, CreateLinkRequest>('/api/links', payload);
  }

  /** Non-paginated list — kept for dashboard / small consumers. */
  list(filters?: ListLinksFilters | null): Observable<LinkResponse[]> {
    return this.listPaged({ ...(filters ?? {}), page: 1, pageSize: 100 }).pipe(
      map((result) => result.items),
    );
  }

  /** Paginated list used by the All Links page. */
  listPaged(filters?: PagedLinksFilters | null): Observable<PagedLinksResponse> {
    const params = buildParams(filters);
    if (filters?.page != null && filters.page > 0) params['page'] = String(filters.page);
    if (filters?.pageSize != null && filters.pageSize > 0)
      params['pageSize'] = String(filters.pageSize);
    const options = Object.keys(params).length > 0 ? { params } : undefined;
    return this.api.get<PagedLinksResponse>('/api/links', options);
  }

  archive(id: string): Observable<void> {
    return this.api.post<void, null>(`/api/links/${encodeURIComponent(id)}/archive`, null);
  }

  restore(id: string): Observable<void> {
    return this.api.post<void, null>(`/api/links/${encodeURIComponent(id)}/restore`, null);
  }

  getById(id: string): Observable<LinkResponse> {
    return this.api.get<LinkResponse>(`/api/links/${encodeURIComponent(id)}`);
  }

  update(id: string, payload: UpdateLinkRequest): Observable<LinkResponse> {
    return this.api.put<LinkResponse, UpdateLinkRequest>(
      `/api/links/${encodeURIComponent(id)}`,
      payload,
    );
  }

  remove(id: string): Observable<void> {
    return this.api.delete<void>(`/api/links/${encodeURIComponent(id)}`);
  }

  /** Import bookmarks from a Netscape HTML file or a Favourites JSON export. */
  importBookmarks(file: File): Observable<{ created: number; skipped: number }> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.api.postForm<{ created: number; skipped: number }>('/api/links/import', form);
  }

  /** Download the user's full link library as JSON or Netscape bookmark HTML. */
  exportLinks(format: 'json' | 'html'): Observable<Blob> {
    return this.api.getBlob(`/api/links/export?format=${format}`);
  }

  /** Archived links older than 90 days (cleanup suggestions). */
  cleanupSuggestions(): Observable<import('../models/link.models').LinkResponse[]> {
    return this.api.get<import('../models/link.models').LinkResponse[]>('/api/links/cleanup-suggestions');
  }
}

function buildParams(filters?: ListLinksFilters | null): Record<string, string> {
  const params: Record<string, string> = {};
  const search = filters?.search?.trim();
  if (search) params['search'] = search;
  if (filters?.tagId) params['tagId'] = filters.tagId;
  if (filters?.categoryId) params['categoryId'] = filters.categoryId;
  if (filters?.sort) params['sort'] = filters.sort;
  if (filters?.archived) params['archived'] = filters.archived;
  if (filters?.archivedFrom) params['archivedFrom'] = filters.archivedFrom;
  if (filters?.archivedTo) params['archivedTo'] = filters.archivedTo;
  return params;
}
