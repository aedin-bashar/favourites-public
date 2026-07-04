import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import type { ArchivedSummaryResponse } from '../models/archived.models';

/**
 * HTTP client for the `/api/archived` and bulk `/api/links/*` endpoints
 *.
 */
@Injectable({ providedIn: 'root' })
export class ArchivedApiService {
  private readonly api = inject(ApiClient);

  /** Aggregate counts for the Archived page stat cards. */
  summary(): Observable<ArchivedSummaryResponse> {
    return this.api.get<ArchivedSummaryResponse>('/api/archived/summary');
  }

  /** Restore a batch of archived links by their IDs. */
  restoreMany(linkIds: string[]): Observable<{ restored: number }> {
    return this.api.post<{ restored: number }, { linkIds: string[] }>(
      '/api/links/restore-many',
      { linkIds },
    );
  }

  /** Permanently delete all archived links for the current user. */
  deleteAllArchived(): Observable<{ deleted: number }> {
    return this.api.delete<{ deleted: number }>('/api/links/archived');
  }
}

