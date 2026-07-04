import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';

import { defaultUserPreferences } from '../../settings/models/user-preferences.models';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import { TagsApiService } from '../services/tags-api.service';
import { TagsPageComponent } from './tags-page.component';
import type { PagedTagsResponse, TagResponse, TagsSummaryResponse } from '../models/tag.models';

const EMPTY_SUMMARY: TagsSummaryResponse = {
  totalTags: 0,
  unusedTags: 0,
  mostUsed: null,
  recentlyAdded: null,
  possibleDuplicates: 0,
};

const EMPTY_PAGED: PagedTagsResponse = { items: [], total: 0, page: 1, pageSize: 25 };

function makeTag(id: string, name: string, linkCount = 0): TagResponse {
  return {
    id,
    name,
    linkCount,
    createdAtUtc: '2026-05-01T12:00:00.000Z',
    lastUsedAtUtc: linkCount > 0 ? '2026-05-02T12:00:00.000Z' : null,
  };
}

function pageText(fixture: { nativeElement: HTMLElement }): string {
  return fixture.nativeElement.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function mockPreferencesProvider() {
  return {
    provide: UserPreferencesService,
    useValue: { preferences: signal(defaultUserPreferences) },
  };
}

function makeService(
  listResult: PagedTagsResponse | Subject<PagedTagsResponse>,
  summaryResult: TagsSummaryResponse = EMPTY_SUMMARY,
) {
  return {
    list: vi.fn(() =>
      listResult instanceof Subject ? listResult.asObservable() : of(listResult),
    ),
    summary: vi.fn(() => of(summaryResult)),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  };
}

describe('TagsPageComponent', () => {
  it('shows the list loading state while tags are in flight', async () => {
    const tags$ = new Subject<PagedTagsResponse>();

    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [provideRouter([]), mockPreferencesProvider(), { provide: TagsApiService, useValue: makeService(tags$) }],
    }).compileComponents();

    const fixture = TestBed.createComponent(TagsPageComponent);
    fixture.detectChanges();

    expect(pageText(fixture)).toContain('Loading tags');
  });

  it('renders the empty state when no tags exist', async () => {
    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [provideRouter([]), mockPreferencesProvider(), { provide: TagsApiService, useValue: makeService(EMPTY_PAGED) }],
    }).compileComponents();

    const fixture = TestBed.createComponent(TagsPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('No tags yet');
    expect(text).toContain('Create tags here, or add them while saving a link.');
  });

  it('renders a retryable friendly load error', async () => {
    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [
        provideRouter([]),
        mockPreferencesProvider(),
        {
          provide: TagsApiService,
          useValue: {
            list: vi.fn(() => throwError(() => ({ status: 0 }))),
            summary: vi.fn(() => of(EMPTY_SUMMARY)),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(TagsPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Could not reach the server. Check your connection and try again.');
    expect(text).toContain('Try again');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('renders the page header with title and actions', async () => {
    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [provideRouter([]), mockPreferencesProvider(), { provide: TagsApiService, useValue: makeService(EMPTY_PAGED) }],
    }).compileComponents();

    const fixture = TestBed.createComponent(TagsPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Tags');
    expect(text).toContain('Organize your saved links with simple, reusable labels.');
    expect(text).toContain('New tag');
    expect(text).toContain('Manage duplicates');
  });

  it('renders stat cards area', async () => {
    const summary: TagsSummaryResponse = {
      totalTags: 5,
      unusedTags: 2,
      mostUsed: { id: 'abc', name: 'Work', count: 10 },
      recentlyAdded: { id: 'xyz', name: 'Travel' },
      possibleDuplicates: 0,
    };

    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [provideRouter([]), mockPreferencesProvider(), { provide: TagsApiService, useValue: makeService(EMPTY_PAGED, summary) }],
    }).compileComponents();

    const fixture = TestBed.createComponent(TagsPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Total tags');
    expect(text).toContain('Most used');
    expect(text).toContain('Unused tags');
    expect(text).toContain('Recently added');
  });

  it('renders the list view table with tags', async () => {
    const paged: PagedTagsResponse = {
      items: [
        makeTag('1', 'Angular', 2),
        makeTag('2', 'TypeScript'),
      ],
      total: 2,
      page: 1,
      pageSize: 25,
    };

    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [provideRouter([]), mockPreferencesProvider(), { provide: TagsApiService, useValue: makeService(paged) }],
    }).compileComponents();

    const fixture = TestBed.createComponent(TagsPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Angular');
    expect(text).toContain('TypeScript');
  });

  it('opens the new tag modal when New tag is clicked', async () => {
    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [provideRouter([]), mockPreferencesProvider(), { provide: TagsApiService, useValue: makeService(EMPTY_PAGED) }],
    }).compileComponents();

    const fixture = TestBed.createComponent(TagsPageComponent);
    fixture.detectChanges();

    const btn = fixture.nativeElement.querySelector(
      'button.fav-btn--primary',
    ) as HTMLButtonElement;
    btn?.click();
    fixture.detectChanges();

    expect(pageText(fixture)).toContain('New tag');
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).not.toBeNull();
  });

  it('renders right-rail TAG HEALTH widget', async () => {
    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [provideRouter([]), mockPreferencesProvider(), { provide: TagsApiService, useValue: makeService(EMPTY_PAGED) }],
    }).compileComponents();

    const fixture = TestBed.createComponent(TagsPageComponent);
    fixture.detectChanges();

    expect(pageText(fixture)).toContain('TAG HEALTH');
    expect(pageText(fixture)).toContain('POPULAR TAGS');
  });
});
