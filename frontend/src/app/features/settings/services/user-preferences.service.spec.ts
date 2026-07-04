import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import { defaultUserPreferences } from '../models/user-preferences.models';
import { UserPreferencesService } from './user-preferences.service';

describe('UserPreferencesService', () => {
  afterEach(() => {
    localStorage.removeItem('fav_user_preferences');
    const root = document.documentElement;
    delete root.dataset['favTheme'];
    delete root.dataset['bsTheme'];
    delete root.dataset['favDensity'];
  });

  function setup(apiOverrides: Partial<Record<'get' | 'patch', unknown>> = {}) {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ApiClient,
          useValue: {
            get: vi.fn(() => of(defaultUserPreferences)),
            patch: vi.fn((_path: string, body: unknown) =>
              of({ ...defaultUserPreferences, ...(body as object) }),
            ),
            delete: vi.fn(() => of(void 0)),
            ...apiOverrides,
          },
        },
      ],
    });
    return TestBed.inject(UserPreferencesService);
  }

  it('applies the light theme and density to <html> on construction', () => {
    setup();
    const root = document.documentElement;
    expect(root.dataset['favTheme']).toBe('light');
    expect(root.dataset['bsTheme']).toBe('light');
    expect(root.dataset['favDensity']).toBe('comfortable');
  });

  it('applies dark theme and compact density when preferences are saved', () => {
    const service = setup();
    service.saveLocalPreferences({
      ...defaultUserPreferences,
      theme: 'dark',
      density: 'compact',
    });

    const root = document.documentElement;
    expect(root.dataset['favTheme']).toBe('dark');
    expect(root.dataset['bsTheme']).toBe('dark');
    expect(root.dataset['favDensity']).toBe('compact');
  });

  it('exposes saved preferences through the reactive signal', () => {
    const service = setup();
    service.saveLocalPreferences({
      ...defaultUserPreferences,
      confirmBeforeDelete: false,
      defaultCategoryId: 'cat-1',
    });

    expect(service.preferences().confirmBeforeDelete).toBe(false);
    expect(service.preferences().defaultCategoryId).toBe('cat-1');
  });

  it('resets to defaults when local preferences are cleared', () => {
    const service = setup();
    service.saveLocalPreferences({ ...defaultUserPreferences, theme: 'dark' });
    service.clearLocalPreferences();

    expect(service.preferences().theme).toBe('light');
    expect(document.documentElement.dataset['favTheme']).toBe('light');
  });
});
