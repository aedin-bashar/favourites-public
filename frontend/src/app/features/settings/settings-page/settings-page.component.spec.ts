import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { ArchivedApiService } from '../../archived/services/archived-api.service';
import { CategoriesApiService } from '../../categories/services/categories-api.service';
import { DashboardApiService } from '../../dashboard/services/dashboard-api.service';
import {
  defaultUserPreferences,
  type PatchUserPreferencesRequest,
} from '../models/user-preferences.models';
import { UserPreferencesService } from '../services/user-preferences.service';
import { SettingsPageComponent } from './settings-page.component';

function pageText(fixture: { nativeElement: HTMLElement }): string {
  return fixture.nativeElement.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

describe('SettingsPageComponent', () => {
  function setup() {
    const preferencesService = {
      preferences: signal(defaultUserPreferences),
      loadPreferences: vi.fn(() => of(defaultUserPreferences)),
      savePreferences: vi.fn((preferences: PatchUserPreferencesRequest) =>
        of({ ...defaultUserPreferences, ...preferences, updatedAtUtc: '2026-05-24T00:00:00Z' }),
      ),
      saveLocalPreferences: vi.fn(),
      clearLocalPreferences: vi.fn(),
      updateProfile: vi.fn(() =>
        of({ id: 'user-1', displayName: 'Bashar', email: 'bashar@example.com' }),
      ),
      deleteAccount: vi.fn(() => of(void 0)),
    };

    const archivedApi = {
      deleteAllArchived: vi.fn(() => of({ deleted: 3 })),
    };

    TestBed.configureTestingModule({
      imports: [SettingsPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: signal({ id: 'user-1', displayName: 'Bashar', email: 'bashar@example.com' }),
            refreshCurrentUser: vi.fn(() =>
              of({ id: 'user-1', displayName: 'Bashar', email: 'bashar@example.com' }),
            ),
            clearLocalUser: vi.fn(),
          },
        },
        { provide: UserPreferencesService, useValue: preferencesService },
        {
          provide: DashboardApiService,
          useValue: {
            summary: vi.fn(() =>
              of({
                totalLinks: 40,
                totalTags: 0,
                totalCategories: 0,
                totalArchived: 8,
                thisWeek: {
                  linksAdded: 0,
                  categoriesCreated: 0,
                  tagsCreated: 0,
                  linksArchived: 0,
                },
              }),
            ),
          },
        },
        { provide: CategoriesApiService, useValue: { listAll: vi.fn(() => of([])) } },
        { provide: ArchivedApiService, useValue: archivedApi },
      ],
    });

    const fixture = TestBed.createComponent(SettingsPageComponent);
    fixture.detectChanges();
    return { fixture, preferencesService, archivedApi };
  }

  it('renders the R9 settings sections without the premium card', () => {
    const { fixture } = setup();
    const text = pageText(fixture);

    expect(text).toContain('Profile');
    expect(text).toContain('Preferences');
    expect(text).toContain('Link defaults');
    expect(text).toContain('Import / Export');
    expect(text).toContain('Danger zone');
    expect(text).toContain('48 saved links (including archived)');
    expect(text).not.toContain('/ 1000');
    expect(text).not.toContain('Go Premium');
  });

  it('saves changed preferences through the user preferences endpoint', () => {
    const { fixture, preferencesService } = setup();
    const host = fixture.nativeElement as HTMLElement;
    const darkButton = Array.from(
      host.querySelectorAll<HTMLButtonElement>('.settings-page__segmented button'),
    ).find((button) => button.textContent?.trim() === 'Dark');
    expect(darkButton).toBeTruthy();

    darkButton!.click();
    fixture.detectChanges();

    const saveButton = Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find(
      (button) => button.textContent?.includes('Save changes'),
    );
    expect(saveButton).toBeTruthy();

    saveButton!.click();
    fixture.detectChanges();

    expect(preferencesService.savePreferences).toHaveBeenCalledWith(
      expect.objectContaining({ theme: 'dark' }),
    );
  });

  it('persists the theme immediately when a theme option is clicked', () => {
    const { fixture, preferencesService } = setup();
    const host = fixture.nativeElement as HTMLElement;
    const darkButton = Array.from(
      host.querySelectorAll<HTMLButtonElement>('.settings-page__segmented button'),
    ).find((button) => button.textContent?.trim() === 'Dark');
    expect(darkButton).toBeTruthy();

    darkButton!.click();
    fixture.detectChanges();

    expect(preferencesService.savePreferences).toHaveBeenCalledWith(
      expect.objectContaining({ theme: 'dark' }),
    );
  });

  it('confirms before deleting archived links', () => {
    const { fixture, archivedApi } = setup();
    const host = fixture.nativeElement as HTMLElement;
    const dangerButtons = host.querySelectorAll<HTMLButtonElement>(
      '.settings-page__danger-row .fav-btn--danger',
    );

    dangerButtons[0].click();
    fixture.detectChanges();

    const confirmButton = Array.from(
      host.querySelectorAll<HTMLButtonElement>('.fav-modal__footer .fav-btn--danger'),
    ).find((button) => button.textContent?.includes('Delete archived'));

    expect(confirmButton).toBeTruthy();
    confirmButton!.click();
    fixture.detectChanges();

    expect(archivedApi.deleteAllArchived).toHaveBeenCalled();
  });
});
