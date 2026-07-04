import { Injectable } from '@angular/core';

export type ViewMode = 'list' | 'cards';
export type ViewPageId = 'links' | 'tags' | 'categories' | 'archived';

const STORAGE_PREFIX = 'fav_view_mode_';

@Injectable({ providedIn: 'root' })
export class ViewModeService {
  get(pageId: ViewPageId): ViewMode {
    const stored = localStorage.getItem(STORAGE_PREFIX + pageId);
    return stored === 'cards' ? 'cards' : 'list';
  }

  set(pageId: ViewPageId, mode: ViewMode): void {
    localStorage.setItem(STORAGE_PREFIX + pageId, mode);
  }
}
