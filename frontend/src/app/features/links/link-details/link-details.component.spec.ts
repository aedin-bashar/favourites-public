import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { defaultUserPreferences } from '../../settings/models/user-preferences.models';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import { LinksApiService } from '../services/links-api.service';
import type { LinkResponse } from '../models/link.models';
import { LinkDetailsComponent } from './link-details.component';

const LINK_ID = '5f8f1a34-0000-0000-0000-000000000001';

function makeLink(overrides: Partial<LinkResponse> = {}): LinkResponse {
  return {
    id: LINK_ID,
    url: 'https://example.com/article',
    title: 'Example article',
    description: null,
    isArchived: false,
    createdAtUtc: '2026-05-01T12:00:00.000Z',
    updatedAtUtc: null,
    tags: [],
    category: null,
    ...overrides,
  };
}

function routeWithId(id: string): Pick<ActivatedRoute, 'snapshot'> {
  return {
    snapshot: { data: {}, paramMap: convertToParamMap({ id }) },
  } as Pick<ActivatedRoute, 'snapshot'>;
}

function findActionButton(root: HTMLElement, label: string): HTMLButtonElement | undefined {
  return Array.from(root.querySelectorAll<HTMLButtonElement>('footer button')).find((b) =>
    b.textContent?.includes(label),
  );
}

describe('LinkDetailsComponent archive toggle', () => {
  async function setup(link: LinkResponse, service: Record<string, unknown>) {
    await TestBed.configureTestingModule({
      imports: [LinkDetailsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: routeWithId(link.id) },
        { provide: LinksApiService, useValue: service },
        {
          provide: UserPreferencesService,
          useValue: { preferences: signal(defaultUserPreferences) },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LinkDetailsComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('archives an active link and flips the button to Restore', async () => {
    const service = {
      getById: vi.fn(() => of(makeLink())),
      archive: vi.fn(() => of(void 0)),
      restore: vi.fn(),
      remove: vi.fn(),
    };
    const fixture = await setup(makeLink(), service);
    const root: HTMLElement = fixture.nativeElement;

    const archiveButton = findActionButton(root, 'Archive');
    expect(archiveButton).toBeDefined();
    expect(archiveButton!.disabled).toBe(false);

    archiveButton!.click();
    fixture.detectChanges();

    expect(service.archive).toHaveBeenCalledWith(LINK_ID);
    expect(findActionButton(root, 'Restore')).toBeDefined();
    expect(root.textContent).toContain('Archived');
  });

  it('restores an archived link and flips the button back to Archive', async () => {
    const archived = makeLink({ isArchived: true });
    const service = {
      getById: vi.fn(() => of(archived)),
      archive: vi.fn(),
      restore: vi.fn(() => of(void 0)),
      remove: vi.fn(),
    };
    const fixture = await setup(archived, service);
    const root: HTMLElement = fixture.nativeElement;

    const restoreButton = findActionButton(root, 'Restore');
    expect(restoreButton).toBeDefined();

    restoreButton!.click();
    fixture.detectChanges();

    expect(service.restore).toHaveBeenCalledWith(LINK_ID);
    expect(findActionButton(root, 'Archive')).toBeDefined();
  });

  it('shows the category row when the link has a category', async () => {
    const withCategory = makeLink({
      category: {
        id: '5f8f1a34-0000-0000-0000-0000000000c1',
        name: 'IT',
        color: '#4f6ef7',
        linkCount: 3,
        createdAtUtc: '2026-04-01T12:00:00.000Z',
        lastActivityAtUtc: null,
      },
    });
    const service = {
      getById: vi.fn(() => of(withCategory)),
      archive: vi.fn(),
      restore: vi.fn(),
      remove: vi.fn(),
    };
    const fixture = await setup(withCategory, service);
    const root: HTMLElement = fixture.nativeElement;

    const labels = Array.from(root.querySelectorAll('dt')).map((dt) => dt.textContent?.trim());
    expect(labels).toContain('Category');
    expect(root.querySelector('.fav-badge--category')?.textContent).toContain('IT');
  });

  it('renders no category row when the link has none', async () => {
    const service = {
      getById: vi.fn(() => of(makeLink())),
      archive: vi.fn(),
      restore: vi.fn(),
      remove: vi.fn(),
    };
    const fixture = await setup(makeLink(), service);
    const root: HTMLElement = fixture.nativeElement;

    const labels = Array.from(root.querySelectorAll('dt')).map((dt) => dt.textContent?.trim());
    expect(labels).not.toContain('Category');
  });
});
