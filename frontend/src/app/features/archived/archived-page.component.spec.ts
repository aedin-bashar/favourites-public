import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { CategoriesApiService } from '../categories/services/categories-api.service';
import { LinksApiService } from '../links/services/links-api.service';
import { defaultUserPreferences } from '../settings/models/user-preferences.models';
import { UserPreferencesService } from '../settings/services/user-preferences.service';
import { TagsApiService } from '../tags/services/tags-api.service';
import { ArchivedApiService } from './services/archived-api.service';
import { ArchivedPageComponent } from './archived-page.component';
import type { ArchivedSummaryResponse } from './models/archived.models';

const MOCK_SUMMARY: ArchivedSummaryResponse = {
  archivedLinks: 6,
  archivedThisMonth: 2,
  oldestArchived: {
    id: '1',
    url: 'https://youtube.com',
    title: 'YouTube',
    description: null,
    isArchived: true,
    createdAtUtc: '2026-05-01T12:00:00.000Z',
    updatedAtUtc: '2026-05-02T12:00:00.000Z',
    tags: [],
    category: null,
  },
  restoredRecently: 1,
  cleanupSuggestions: [],
};

function makeCategoriesService() {
  return { listAll: vi.fn(() => of([])), list: vi.fn(() => of({ items: [], total: 0, page: 1, pageSize: 25 })) };
}

function makeTagsService() {
  return { listAll: vi.fn(() => of([]) ) };
}

function makeArchivedService(summary = MOCK_SUMMARY) {
  return { summary: vi.fn(() => of(summary)) };
}

function makeLinksService() {
  return {
    listPaged: vi.fn(() => of({ items: [], total: 0, page: 1, pageSize: 25 })),
    restore: vi.fn(),
    remove: vi.fn(),
    cleanupSuggestions: vi.fn(() => of([])),
  };
}

function pageText(fixture: { nativeElement: HTMLElement }): string {
  return fixture.nativeElement.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

describe('ArchivedPageComponent', () => {
  async function setup(archivedService = makeArchivedService()) {
    await TestBed.configureTestingModule({
      imports: [ArchivedPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        { provide: ArchivedApiService, useValue: archivedService },
        { provide: LinksApiService, useValue: makeLinksService() },
        { provide: CategoriesApiService, useValue: makeCategoriesService() },
        { provide: TagsApiService, useValue: makeTagsService() },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ArchivedPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the page title', async () => {
    const fixture = await setup();
    expect(pageText(fixture)).toContain('Archived');
  });

  it('renders stat cards with values from summary', async () => {
    const fixture = await setup();
    const text = pageText(fixture);
    expect(text).toContain('Archived links');
    expect(text).toContain('Archived this month');
    expect(text).toContain('Oldest archived');
    expect(text).toContain('Restored recently');
    expect(text).toContain('6');
    expect(text).toContain('YouTube');
  });

  it('shows the info banner by default', async () => {
    // Clear any persisted dismissal from previous tests.
    localStorage.removeItem('fav_archived_banner_dismissed');
    const fixture = await setup();
    expect(pageText(fixture)).toContain(
      'Archived links are hidden from your main lists',
    );
  });

  it('renders the filter toolbar on desktop', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('#archived-search')).toBeTruthy();
    expect(el.querySelector('#archived-category-filter')).toBeTruthy();
    expect(el.querySelector('#archived-tag-filter')).toBeTruthy();
    expect(el.querySelector('#archived-date-filter')).toBeTruthy();
    expect(el.querySelector('#archived-sort')).toBeTruthy();
  });

  it('shows zero placeholders when summary call fails', async () => {
    const failingService = { summary: vi.fn(() => throwError(() => new Error('not found'))) };
    const fixture = await setup(failingService);
    const text = pageText(fixture);
    expect(text).toContain('Archived links');
    // With failed summary, all counts fall back to 0 / '—'
    expect(text).toContain('0');
  });

  it('renders header action buttons', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    const buttons = el.querySelectorAll<HTMLButtonElement>('button');
    const labels = Array.from(buttons).map((b) => b.textContent?.trim());
    expect(labels.some((l) => l?.includes('Restore selected'))).toBe(true);
    expect(labels.some((l) => l?.includes('Empty archive'))).toBe(true);
  });
});
