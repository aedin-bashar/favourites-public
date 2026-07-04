import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';

export interface DashboardThisWeek {
  readonly linksAdded: number;
  readonly categoriesCreated: number;
  readonly tagsCreated: number;
  readonly linksArchived: number;
}

export interface DashboardSummary {
  readonly totalLinks: number;
  readonly totalTags: number;
  readonly totalCategories: number;
  readonly totalArchived: number;
  readonly thisWeek: DashboardThisWeek;
}

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly api = inject(ApiClient);

  summary(): Observable<DashboardSummary> {
    return this.api.get<DashboardSummary>('/api/dashboard/summary');
  }
}
