import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { CategoriesApiService } from '../categories/services/categories-api.service';
import { LinksApiService } from '../links/services/links-api.service';
import { defaultUserPreferences } from '../settings/models/user-preferences.models';
import { UserPreferencesService } from '../settings/services/user-preferences.service';
import { TagsApiService } from '../tags/services/tags-api.service';
import { DashboardApiService } from './services/dashboard-api.service';
import { DashboardComponent } from './dashboard.component';
import type { DashboardSummary } from './services/dashboard-api.service';

function pageText(fixture: { nativeElement: HTMLElement }): string {
  return fixture.nativeElement.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

const mockPreferences = { preferences: signal(defaultUserPreferences) };

const emptySummary: DashboardSummary = {
  totalLinks: 0,
  totalTags: 0,
  totalCategories: 0,
  totalArchived: 0,
  thisWeek: { linksAdded: 0, categoriesCreated: 0, tagsCreated: 0, linksArchived: 0 },
};

describe('DashboardComponent UX states', () => {

  it('shows welcome header with display name', async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: AuthService, useValue: { user: signal({ displayName: 'Bashar' }) } },
        { provide: LinksApiService, useValue: { list: vi.fn(() => of([])) } },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: DashboardApiService, useValue: { summary: vi.fn(() => of(emptySummary)) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(pageText(fixture)).toContain('Welcome, Bashar');
  });

  it('shows fallback welcome when not logged in', async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: AuthService, useValue: { user: signal(null) } },
        { provide: LinksApiService, useValue: { list: vi.fn(() => of([])) } },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: DashboardApiService, useValue: { summary: vi.fn(() => of(emptySummary)) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(pageText(fixture)).toContain('Welcome to Favourites');
  });

  it('shows loading state for recent links while in flight', async () => {
    const links = new Subject<never[]>();

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: AuthService, useValue: { user: signal({ displayName: 'Bashar' }) } },
        { provide: LinksApiService, useValue: { list: vi.fn(() => links.asObservable()) } },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: DashboardApiService, useValue: { summary: vi.fn(() => of(emptySummary)) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(pageText(fixture)).toContain('Loading your recent links');
  });

  it('renders empty state for recent links', async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: AuthService, useValue: { user: signal(null) } },
        { provide: LinksApiService, useValue: { list: vi.fn(() => of([])) } },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: DashboardApiService, useValue: { summary: vi.fn(() => of(emptySummary)) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(pageText(fixture)).toContain('No links saved yet');
  });

  it('renders retryable error when links section fails', async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: AuthService, useValue: { user: signal(null) } },
        { provide: LinksApiService, useValue: { list: vi.fn(() => throwError(() => ({ status: 0 }))) } },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: DashboardApiService, useValue: { summary: vi.fn(() => of(emptySummary)) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('Could not reach the server. Check your connection and try again.');
    expect(fixture.nativeElement.querySelectorAll('[role="alert"]').length).toBeGreaterThanOrEqual(1);
  });

  it('renders stat cards with values from the summary endpoint', async () => {
    const testSummary: DashboardSummary = {
      totalLinks: 32,
      totalTags: 8,
      totalCategories: 2,
      totalArchived: 4,
      thisWeek: { linksAdded: 5, categoriesCreated: 1, tagsCreated: 2, linksArchived: 1 },
    };

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: AuthService, useValue: { user: signal({ displayName: 'Bashar' }) } },
        { provide: LinksApiService, useValue: { list: vi.fn(() => of([])) } },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: DashboardApiService, useValue: { summary: vi.fn(() => of(testSummary)) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('32');
    expect(text).toContain('Total links');
    expect(text).toContain('8');
    expect(text).toContain('Tags');
    expect(text).toContain('2');
    expect(text).toContain('Categories');
    expect(text).toContain('4');
    expect(text).toContain('Archived');
  });

  it('renders This week stats from the summary endpoint', async () => {
    const testSummary: DashboardSummary = {
      totalLinks: 10,
      totalTags: 3,
      totalCategories: 1,
      totalArchived: 0,
      thisWeek: { linksAdded: 5, categoriesCreated: 1, tagsCreated: 2, linksArchived: 3 },
    };

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: UserPreferencesService, useValue: mockPreferences },
        { provide: AuthService, useValue: { user: signal({ displayName: 'Bashar' }) } },
        { provide: LinksApiService, useValue: { list: vi.fn(() => of([])) } },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: DashboardApiService, useValue: { summary: vi.fn(() => of(testSummary)) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    const text = pageText(fixture);
    expect(text).toContain('This week');
    expect(text).toContain('Links added');
    expect(text).toContain('Links archived');
  });

  it('quick save honours the default-category and auto-extract-title preferences', async () => {
    const create = vi.fn((payload: unknown) =>
      of({
        id: 'l1',
        url: 'https://example.com/some-article',
        title: 'x',
        description: null,
        isArchived: false,
        createdAtUtc: '2026-07-03T00:00:00Z',
        updatedAtUtc: null,
        tags: [],
        category: null,
        ...(payload as object),
      }),
    );

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        {
          provide: UserPreferencesService,
          useValue: {
            preferences: signal({
              ...defaultUserPreferences,
              autoExtractTitle: false,
              defaultCategoryId: 'cat-42',
            }),
          },
        },
        { provide: AuthService, useValue: { user: signal({ displayName: 'Bashar' }) } },
        { provide: LinksApiService, useValue: { list: vi.fn(() => of([])), create } },
        { provide: TagsApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: DashboardApiService, useValue: { summary: vi.fn(() => of(emptySummary)) } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;

    const urlInput = host.querySelector<HTMLInputElement>('input[type="url"], input[formcontrolname="url"]');
    expect(urlInput).toBeTruthy();
    urlInput!.value = 'https://example.com/some-article';
    urlInput!.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const form = urlInput!.closest('form');
    expect(form).toBeTruthy();
    form!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(create).toHaveBeenCalledWith(
      expect.objectContaining({
        // autoExtractTitle off → raw URL as title, no prettified slug
        title: 'https://example.com/some-article',
        categoryId: 'cat-42',
      }),
    );
  });
});
