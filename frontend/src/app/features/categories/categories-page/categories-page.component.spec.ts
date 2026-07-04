import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';

import { defaultUserPreferences } from '../../settings/models/user-preferences.models';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import { CategoriesApiService } from '../services/categories-api.service';
import { CategoriesPageComponent } from './categories-page.component';
import type {
  CategoriesSummaryResponse,
  CategoryResponse,
  PagedCategoriesResponse,
} from '../models/category.models';

const EMPTY_SUMMARY: CategoriesSummaryResponse = {
  totalCategories: 0,
  emptyCategories: 0,
  largestCategory: null,
  recentlyAdded: null,
  uncategorizedLinks: 0,
};

const EMPTY_PAGED: PagedCategoriesResponse = { items: [], total: 0, page: 1, pageSize: 25 };

function makeCategory(id: string, name: string, linkCount = 0): CategoryResponse {
  return {
    id,
    name,
    color: '#0d6efd',
    linkCount,
    createdAtUtc: '2026-05-01T12:00:00.000Z',
    lastActivityAtUtc: linkCount > 0 ? '2026-05-02T12:00:00.000Z' : null,
  };
}

function pageText(fixture: { nativeElement: HTMLElement }): string {
  return fixture.nativeElement.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function makeService(
  listResult: PagedCategoriesResponse | Subject<PagedCategoriesResponse>,
  summaryResult: CategoriesSummaryResponse = EMPTY_SUMMARY,
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

describe('CategoriesPageComponent UX states', () => {
  it('shows the list loading state while categories are in flight', async () => {
    const categories$ = new Subject<PagedCategoriesResponse>();

    await TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        { provide: CategoriesApiService, useValue: makeService(categories$) },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CategoriesPageComponent);
    fixture.detectChanges();

    expect(pageText(fixture)).toContain('Loading categories...');
  });

  it('renders the empty state when no categories exist', async () => {
    await TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        { provide: CategoriesApiService, useValue: makeService(EMPTY_PAGED) },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CategoriesPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('No categories yet');
    expect(text).toContain('Create categories here, or create them while saving a link.');
  });

  it('renders a retryable friendly load error', async () => {
    await TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        {
          provide: CategoriesApiService,
          useValue: {
            list: vi.fn(() => throwError(() => ({ status: 401 }))),
            summary: vi.fn(() => of(EMPTY_SUMMARY)),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CategoriesPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Your session has expired. Sign in again to manage categories.');
    expect(text).toContain('Try again');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('renders the page header with New category button', async () => {
    await TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        { provide: CategoriesApiService, useValue: makeService(EMPTY_PAGED) },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CategoriesPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Categories');
    expect(text).toContain('Group your saved links into clear collections.');
    expect(text).toContain('New category');
  });

  it('shows stat cards after load', async () => {
    const cats = [
      makeCategory('1', 'Alpha', 5),
      makeCategory('2', 'Beta'),
    ];
    const summary: CategoriesSummaryResponse = {
      totalCategories: 2,
      emptyCategories: 1,
      largestCategory: { id: '1', name: 'Alpha', count: 5 },
      recentlyAdded: { id: '2', name: 'Beta' },
      uncategorizedLinks: 1,
    };

    await TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        {
          provide: CategoriesApiService,
          useValue: makeService({ items: cats, total: cats.length, page: 1, pageSize: 25 }, summary),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CategoriesPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Total categories');
    expect(text).toContain('2');
    expect(text).toContain('Alpha'); // largest category name
    expect(text).toContain('Beta');  // recently added name
  });

  it('renders the list view table with Name, Links, Created, Last activity columns', async () => {
    const cats = [
      makeCategory('1', 'Alpha'),
      makeCategory('2', 'Beta'),
    ];

    await TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        {
          provide: CategoriesApiService,
          useValue: makeService({ items: cats, total: cats.length, page: 1, pageSize: 25 }),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CategoriesPageComponent);
    fixture.detectChanges();

    // Default view mode is 'list', so the table should be visible
    const table = fixture.nativeElement.querySelector('.categories-page__table');
    expect(table).not.toBeNull();

    const headers = Array.from<Element>(table.querySelectorAll('th')).map((th) =>
      th.textContent?.trim(),
    );
    expect(headers).toContain('Name');
    expect(headers).toContain('Links');
    expect(headers).toContain('Created');
    expect(headers).toContain('Last activity');

    // Each row should show the category name
    const rows = table.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Alpha');
    expect(rows[1].textContent).toContain('Beta');
  });

  it('renders right rail widgets: Category health, Largest categories, Quick actions', async () => {
    const cats = [
      makeCategory('1', 'News', 3),
      makeCategory('2', 'Tech', 2),
    ];

    await TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        {
          provide: CategoriesApiService,
          useValue: makeService({ items: cats, total: cats.length, page: 1, pageSize: 25 }),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CategoriesPageComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('CATEGORY HEALTH');
    expect(text).toContain('Empty categories');
    expect(text).toContain('Uncategorized links');
    expect(text).toContain('Categories updated');
    expect(text).toContain('LARGEST CATEGORIES');
    expect(text).toContain('News');
  });

  it('creates a category when the New category form is submitted through the template', async () => {
    const service = makeService(EMPTY_PAGED);
    service.create.mockReturnValue(of(makeCategory('9', 'Work')));

    await TestBed.configureTestingModule({
      imports: [CategoriesPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
        { provide: CategoriesApiService, useValue: service },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CategoriesPageComponent);
    fixture.detectChanges();

    const root: HTMLElement = fixture.nativeElement;
    const openButton = Array.from(root.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('New category'),
    );
    expect(openButton).toBeDefined();
    openButton!.click();
    fixture.detectChanges();

    const nameInput = root.querySelector<HTMLInputElement>('#new-category-name');
    expect(nameInput).not.toBeNull();
    nameInput!.value = 'Work';
    nameInput!.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const form = root.querySelector<HTMLFormElement>('[role="dialog"] form');
    expect(form).not.toBeNull();
    // Dispatch the native submit event (what Enter / a type="submit" button
    // triggers) — this is the path that silently reloaded the page before.
    const submitEvent = new Event('submit', { cancelable: true });
    form!.dispatchEvent(submitEvent);
    fixture.detectChanges();

    expect(service.create).toHaveBeenCalledWith({ name: 'Work' });
    expect(submitEvent.defaultPrevented).toBe(true);
    // Modal closes on success.
    expect(root.querySelector('#new-category-name')).toBeNull();
  });
});
