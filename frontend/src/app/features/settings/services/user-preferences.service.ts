import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, of, tap, type Observable } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client.service';
import {
  defaultUserPreferences,
  type PatchUserPreferencesRequest,
  type UpdateProfileRequest,
  type UserPreferences,
  type UserProfileResponse,
} from '../models/user-preferences.models';

const STORAGE_KEY = 'fav_user_preferences';

@Injectable({ providedIn: 'root' })
export class UserPreferencesService {
  private readonly api = inject(ApiClient);
  private readonly document = inject(DOCUMENT);

  /**
   * Reactive source of truth for the current user's preferences.
   *
   * Seeded synchronously from localStorage so consumers (quick save, link
   * cards, sort defaults, …) have sensible values before the API responds;
   * refreshed by {@link loadPreferences} (called once from the authenticated
   * app layout) and by every save.
   */
  private readonly preferencesState = signal<UserPreferences>(defaultUserPreferences);
  readonly preferences = this.preferencesState.asReadonly();

  private readonly systemDark =
    this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)') ?? null;

  constructor() {
    this.setPreferences(this.loadLocal(), { persist: false });
    // Re-evaluate the effective theme when the OS scheme flips while the
    // user is on "System".
    this.systemDark?.addEventListener('change', () => {
      if (this.preferencesState().theme === 'system') {
        this.applyToDocument(this.preferencesState());
      }
    });
  }

  loadPreferences(): Observable<UserPreferences> {
    return this.api.get<UserPreferences>('/api/user/preferences').pipe(
      tap((preferences) => this.setPreferences(preferences)),
      catchError(() => {
        const local = this.loadLocal();
        this.setPreferences(local, { persist: false });
        return of(local);
      }),
    );
  }

  savePreferences(preferences: PatchUserPreferencesRequest): Observable<UserPreferences> {
    this.setPreferences({ ...preferences, updatedAtUtc: new Date().toISOString() });

    return this.api.patch<UserPreferences, PatchUserPreferencesRequest>(
      '/api/user/preferences',
      preferences,
    ).pipe(tap((saved) => this.setPreferences(saved)));
  }

  saveLocalPreferences(preferences: PatchUserPreferencesRequest): void {
    this.setPreferences({ ...preferences, updatedAtUtc: new Date().toISOString() });
  }

  clearLocalPreferences(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Ignore storage failures; backend persistence remains the source of truth.
    }
    this.setPreferences(defaultUserPreferences, { persist: false });
  }

  updateProfile(payload: UpdateProfileRequest): Observable<UserProfileResponse> {
    return this.api.patch<UserProfileResponse, UpdateProfileRequest>('/api/user/profile', payload);
  }

  deleteAccount(): Observable<void> {
    return this.api.delete<void>('/api/user/account');
  }

  private setPreferences(
    preferences: UserPreferences,
    options: { persist: boolean } = { persist: true },
  ): void {
    if (options.persist) {
      this.persistLocal(preferences);
    }
    this.preferencesState.set(preferences);
    this.applyToDocument(preferences);
  }

  private loadLocal(): UserPreferences {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return defaultUserPreferences;
      return { ...defaultUserPreferences, ...JSON.parse(raw) } as UserPreferences;
    } catch {
      return defaultUserPreferences;
    }
  }

  private persistLocal(preferences: UserPreferences): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences));
    } catch {
      // Local persistence is a convenience layer; failures should not block the UI.
    }
  }

  private applyToDocument(preferences: UserPreferences): void {
    const effectiveTheme =
      preferences.theme === 'dark' ||
      (preferences.theme === 'system' && (this.systemDark?.matches ?? false))
        ? 'dark'
        : 'light';
    const root = this.document.documentElement;
    root.dataset['favTheme'] = effectiveTheme;
    // Bootstrap 5.3 reads data-bs-theme for its own component palette.
    root.dataset['bsTheme'] = effectiveTheme;
    root.dataset['favDensity'] = preferences.density;
    root.style.setProperty('--fav-theme-mode', effectiveTheme);
    root.style.setProperty('--fav-density-mode', preferences.density);
  }
}
