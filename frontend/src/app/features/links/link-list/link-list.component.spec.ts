import { signal } from '@angular/core';
import { convertToParamMap } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';

import { CategoriesApiService } from '../../categories/services/categories-api.service';
import { defaultUserPreferences } from '../../settings/models/user-preferences.models';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import { TagsApiService } from '../../tags/services/tags-api.service';
import { LinksApiService } from '../services/links-api.service';
import type { PagedLinksResponse } from '../services/links-api.service';
import { LinkListComponent } from './link-list.component';

function pageText(fixture: { nativeElement: HTMLElement }): string {
  return fixture.nativeElement.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

const mockPreferences = { preferences: signal(defaultUserPreferences) };

function routeWithQuery(
  query: Record<string, string> = {},
): Pick<ActivatedRoute, 'queryParamMap' | 'snapshot'> {
  const queryParamMap = convertToParamMap(query);

  return {
    queryParamMap: of(queryParamMap),
    snapshot: {
      data: {},
      queryParamMap,
    },
  } as Pick<ActivatedRoute, 'queryParamMap' | 'snapshot'>;
}

const emptyPage: PagedLinksResponse = { items: [], total: 0, page: 1, pageSize: 25 };

describe('LinkListComponent UX states', () => {
  it('shows loading state while links are being fetched', async () => {
    const pagedLinks = new Subject<PagedLinksResponse>();
    const tags = new Subject<never[]>();
    const categories = new Subject<never[]>();

    await TestBed.configureTestingModule({
      imports: [LinkListComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: ActivatedRoute, useValue: routeWithQuery() },
        {
          provide: LinksApiService,
          useValue: {
            listPaged: vi.fn(() => pagedLinks.asObservable()),
            archive: vi.fn(),
            restore: vi.fn(),
            remove: vi.fn(),
          },
        },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => tags.asObservable()) } },
        {
          provide: CategoriesApiService,
          useValue: { listAll: vi.fn(() => categories.asObservable()) },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LinkListComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Loading your links');
  });

  it('renders the active-list empty state when there are no saved links', async () => {
    await TestBed.configureTestingModule({
      imports: [LinkListComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: ActivatedRoute, useValue: routeWithQuery() },
        {
          provide: LinksApiService,
          useValue: {
            listPaged: vi.fn(() => of(emptyPage)),
            archive: vi.fn(),
            restore: vi.fn(),
            remove: vi.fn(),
          },
        },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LinkListComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('No links to show yet');
    expect(text).toContain('Add your first link');
  });

  it('renders the search-results empty state when a query has no matches', async () => {
    await TestBed.configureTestingModule({
      imports: [LinkListComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: ActivatedRoute, useValue: routeWithQuery({ search: 'Angular' }) },
        {
          provide: LinksApiService,
          useValue: {
            listPaged: vi.fn(() => of(emptyPage)),
            archive: vi.fn(),
            restore: vi.fn(),
            remove: vi.fn(),
          },
        },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LinkListComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('No links match');
    expect(text).toContain('Angular');
    expect(text).toContain('clear all filters');
  });

  it('renders retryable list and filter errors', async () => {
    await TestBed.configureTestingModule({
      imports: [LinkListComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: ActivatedRoute, useValue: routeWithQuery() },
        {
          provide: LinksApiService,
          useValue: {
            listPaged: vi.fn(() => throwError(() => ({ status: 401 }))),
            archive: vi.fn(),
            restore: vi.fn(),
            remove: vi.fn(),
          },
        },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => throwError(() => ({ status: 0 }))) } },
        {
          provide: CategoriesApiService,
          useValue: { listAll: vi.fn(() => throwError(() => ({ status: 500 }))) },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LinkListComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Your session has expired. Sign in again to view your links.');
    expect(text).toContain('Could not reach the server to load tag filters.');
    expect(text).toContain('Category filters are unavailable right now.');
    expect(text).toContain('Try again');
    expect(text).toContain('Retry filters');
  });

  it('renders the page header with All Links title and action buttons', async () => {
    await TestBed.configureTestingModule({
      imports: [LinkListComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: ActivatedRoute, useValue: routeWithQuery() },
        {
          provide: LinksApiService,
          useValue: {
            listPaged: vi.fn(() => of(emptyPage)),
            archive: vi.fn(),
            restore: vi.fn(),
            remove: vi.fn(),
          },
        },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LinkListComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('All Links');
    expect(text).toContain('Add link');
    expect(text).toContain('Import bookmarks');
  });

  it('deletes without the confirm modal when "Confirm before delete" is off', async () => {
    const link = {
      id: 'l1',
      url: 'https://example.com',
      title: 'Example',
      description: null,
      isArchived: false,
      createdAtUtc: '2026-07-01T00:00:00Z',
      updatedAtUtc: null,
      tags: [],
      category: null,
    };
    const remove = vi.fn(() => of(void 0));

    await TestBed.configureTestingModule({
      imports: [LinkListComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: {
            preferences: signal({ ...defaultUserPreferences, confirmBeforeDelete: false }),
          },
        },
        { provide: ActivatedRoute, useValue: routeWithQuery() },
        {
          provide: LinksApiService,
          useValue: {
            listPaged: vi.fn(() => of({ items: [link], total: 1, page: 1, pageSize: 25 })),
            archive: vi.fn(),
            restore: vi.fn(),
            remove,
          },
        },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LinkListComponent);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;

    const deleteButton = host.querySelector<HTMLButtonElement>('button[aria-label="Delete Example"]');
    expect(deleteButton).toBeTruthy();

    deleteButton!.click();
    fixture.detectChanges();

    // No modal, straight to the API.
    expect(host.querySelector('.fav-modal')).toBeNull();
    expect(remove).toHaveBeenCalledWith('l1');
  });
});
