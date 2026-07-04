import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostListener,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { FavIcons } from '../../../shared/icons/fav-icons';
import { ArchivedApiService } from '../../archived/services/archived-api.service';
import { CategoriesApiService } from '../../categories/services/categories-api.service';
import type { CategoryResponse } from '../../categories/models/category.models';
import type { DashboardSummary } from '../../dashboard/services/dashboard-api.service';
import { DashboardApiService } from '../../dashboard/services/dashboard-api.service';
import { LinksApiService } from '../../links/services/links-api.service';
import { ImportBookmarksModalComponent } from '../../links/import-bookmarks-modal/import-bookmarks-modal.component';
import { FocusTrapDirective } from '../../../shared/directives/focus-trap.directive';
import {
  defaultUserPreferences,
  type CategoriesDefaultSort,
  type DensityMode,
  type PatchUserPreferencesRequest,
  type TagsDefaultSort,
  type ThemeMode,
  type UserPreferences,
} from '../models/user-preferences.models';
import { UserPreferencesService } from '../services/user-preferences.service';

type SettingsTabId =
  | 'profile'
  | 'preferences'
  | 'link-defaults'
  | 'tags-categories'
  | 'import-export'
  | 'danger-zone';

interface SettingsTab {
  readonly id: SettingsTabId;
  readonly label: string;
  readonly icon: string;
}

const SETTINGS_TABS: readonly SettingsTab[] = [
  { id: 'profile', label: 'Profile', icon: 'fa-solid fa-user' },
  { id: 'preferences', label: 'Preferences', icon: 'fa-solid fa-sliders' },
  { id: 'link-defaults', label: 'Link defaults', icon: 'fa-solid fa-link' },
  { id: 'tags-categories', label: 'Tags & categories', icon: 'fa-solid fa-tags' },
  { id: 'import-export', label: 'Import / Export', icon: 'fa-solid fa-download' },
  { id: 'danger-zone', label: 'Danger zone', icon: 'fa-solid fa-triangle-exclamation' },
];

const THEME_OPTIONS: readonly { value: ThemeMode; label: string }[] = [
  { value: 'light', label: 'Light' },
  { value: 'system', label: 'System' },
  { value: 'dark', label: 'Dark' },
];

const DENSITY_OPTIONS: readonly { value: DensityMode; label: string }[] = [
  { value: 'comfortable', label: 'Comfortable' },
  { value: 'compact', label: 'Compact' },
];

const TAG_SORT_OPTIONS: readonly { value: TagsDefaultSort; label: string }[] = [
  { value: 'name', label: 'Name' },
  { value: 'most-used', label: 'Most used' },
  { value: 'newest', label: 'Newest' },
];

const CATEGORY_SORT_OPTIONS: readonly { value: CategoriesDefaultSort; label: string }[] = [
  { value: 'name', label: 'Name' },
  { value: 'largest', label: 'Largest' },
  { value: 'newest', label: 'Newest' },
  { value: 'recently-active', label: 'Recently active' },
];

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [ReactiveFormsModule, ImportBookmarksModalComponent, FocusTrapDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings-page.component.html',
  styleUrl: './settings-page.component.scss',
})
export class SettingsPageComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly preferencesService = inject(UserPreferencesService);
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly archivedApi = inject(ArchivedApiService);
  private readonly linksApi = inject(LinksApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly icons = FavIcons;
  protected readonly tabs = SETTINGS_TABS;
  protected readonly themeOptions = THEME_OPTIONS;
  protected readonly densityOptions = DENSITY_OPTIONS;
  protected readonly tagSortOptions = TAG_SORT_OPTIONS;
  protected readonly categorySortOptions = CATEGORY_SORT_OPTIONS;

  protected readonly activeTab = signal<SettingsTabId>('profile');
  protected readonly categories = signal<CategoryResponse[]>([]);
  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly summaryLoading = signal(true);

  protected readonly preferencesLoading = signal(true);
  protected readonly preferencesSaving = signal(false);
  protected readonly preferencesDirty = signal(false);
  protected readonly preferencesMessage = signal<string | null>(null);
  protected readonly preferencesError = signal<string | null>(null);

  protected readonly profileSaving = signal(false);
  protected readonly profileMessage = signal<string | null>(null);
  protected readonly profileError = signal<string | null>(null);
  protected readonly profileDisplayName = signal('');
  protected readonly avatarInitial = computed(() => {
    const value = this.profileDisplayName().trim();
    return (value[0] ?? '?').toUpperCase();
  });

  protected readonly dangerMessage = signal<string | null>(null);
  protected readonly dangerError = signal<string | null>(null);
  protected readonly deleteArchivedModalOpen = signal(false);
  protected readonly deleteAccountModalOpen = signal(false);
  protected readonly deletingArchived = signal(false);
  protected readonly deletingAccount = signal(false);

  // Import bookmarks modal (from Settings) — HTML or JSON mode
  protected readonly settingsImportModalOpen = signal(false);
  protected readonly settingsImportFormat = signal<'html' | 'json'>('html');
  // Export busy states
  protected readonly exportingJson = signal(false);
  protected readonly exportingHtml = signal(false);

  protected readonly usedLinks = computed(
    () => (this.summary()?.totalLinks ?? 0) + (this.summary()?.totalArchived ?? 0),
  );

  protected readonly profileForm = new FormGroup({
    displayName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    email: new FormControl<string>({ value: '', disabled: true }, { nonNullable: true }),
  });

  protected readonly settingsForm = new FormGroup({
    theme: new FormControl<ThemeMode>(defaultUserPreferences.theme, { nonNullable: true }),
    density: new FormControl<DensityMode>(defaultUserPreferences.density, { nonNullable: true }),
    defaultCategoryId: new FormControl<string | null>(defaultUserPreferences.defaultCategoryId),
    autoExtractTitle: new FormControl<boolean>(defaultUserPreferences.autoExtractTitle, {
      nonNullable: true,
    }),
    showFavicon: new FormControl<boolean>(defaultUserPreferences.showFavicon, {
      nonNullable: true,
    }),
    openInNewTab: new FormControl<boolean>(defaultUserPreferences.openInNewTab, {
      nonNullable: true,
    }),
    confirmBeforeDelete: new FormControl<boolean>(defaultUserPreferences.confirmBeforeDelete, {
      nonNullable: true,
    }),
    tagsDefaultSort: new FormControl<TagsDefaultSort>(defaultUserPreferences.tagsDefaultSort, {
      nonNullable: true,
    }),
    categoriesDefaultSort: new FormControl<CategoriesDefaultSort>(
      defaultUserPreferences.categoriesDefaultSort,
      { nonNullable: true },
    ),
  });

  private applyingPreferences = false;
  /** Bumped on every form edit so an in-flight save never clobbers newer edits. */
  private preferencesEditVersion = 0;

  ngOnInit(): void {
    this.loadCurrentUser();
    this.loadPreferences();
    this.loadCategories();
    this.loadSummary();

    this.profileForm.controls.displayName.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.profileDisplayName.set(value));

    this.settingsForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.applyingPreferences) return;
        this.preferencesEditVersion++;
        this.preferencesDirty.set(true);
        this.preferencesMessage.set(null);
        this.preferencesError.set(null);
        this.preferencesService.saveLocalPreferences(this.preferencesPayload());
      });
  }

  protected setActiveTab(tab: SettingsTabId, event?: Event): void {
    event?.preventDefault();
    this.activeTab.set(tab);
    document.getElementById(`settings-${tab}`)?.scrollIntoView({
      block: 'start',
      behavior: 'smooth',
    });
  }

  protected onMobileTabChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as SettingsTabId;
    this.setActiveTab(value);
  }

  protected setTheme(theme: ThemeMode): void {
    this.settingsForm.controls.theme.setValue(theme);
    // Appearance changes apply instantly, so persist them instantly too —
    // otherwise the next loadPreferences() reverts to the server value.
    this.savePreferences();
  }

  protected setDensity(density: DensityMode): void {
    this.settingsForm.controls.density.setValue(density);
    this.savePreferences();
  }

  protected savePreferences(): void {
    if (this.preferencesSaving()) return;

    this.preferencesSaving.set(true);
    this.preferencesError.set(null);
    this.preferencesMessage.set(null);

    const versionAtSave = this.preferencesEditVersion;

    this.preferencesService
      .savePreferences(this.preferencesPayload())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (preferences) => {
          this.preferencesSaving.set(false);
          this.preferencesMessage.set('Settings saved.');
          // Only sync the form back when nothing changed while the save was
          // in flight; otherwise keep the newer edits and the dirty flag.
          if (this.preferencesEditVersion === versionAtSave) {
            this.preferencesDirty.set(false);
            this.applyPreferencesToForm(preferences);
          }
        },
        error: (err: unknown) => {
          this.preferencesSaving.set(false);
          this.preferencesError.set(formatSettingsError(err));
        },
      });
  }

  protected resetPreferences(): void {
    if (this.preferencesSaving()) return;
    this.preferencesService.clearLocalPreferences();
    this.loadPreferences();
    this.preferencesMessage.set('Changes reset.');
  }

  protected updateProfile(): void {
    if (this.profileForm.invalid || this.profileSaving()) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.profileSaving.set(true);
    this.profileError.set(null);
    this.profileMessage.set(null);

    this.preferencesService
      .updateProfile({ displayName: this.profileForm.controls.displayName.value.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          this.profileSaving.set(false);
          this.profileMessage.set('Profile updated.');
          this.applyProfile(profile.displayName, profile.email);
          this.auth.refreshCurrentUser().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
        },
        error: (err: unknown) => {
          this.profileSaving.set(false);
          this.profileError.set(formatSettingsError(err));
        },
      });
  }

  protected openDeleteArchivedModal(): void {
    this.dangerError.set(null);
    this.dangerMessage.set(null);
    this.deleteArchivedModalOpen.set(true);
  }

  protected closeDeleteArchivedModal(): void {
    if (this.deletingArchived()) return;
    this.deleteArchivedModalOpen.set(false);
  }

  protected confirmDeleteArchived(): void {
    if (this.deletingArchived()) return;

    this.deletingArchived.set(true);
    this.dangerError.set(null);
    this.archivedApi
      .deleteAllArchived()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.deletingArchived.set(false);
          this.deleteArchivedModalOpen.set(false);
          this.dangerMessage.set(`${result.deleted} archived links deleted.`);
          this.loadSummary();
        },
        error: (err: unknown) => {
          this.deletingArchived.set(false);
          this.dangerError.set(formatSettingsError(err));
        },
      });
  }

  protected openDeleteAccountModal(): void {
    this.dangerError.set(null);
    this.dangerMessage.set(null);
    this.deleteAccountModalOpen.set(true);
  }

  protected closeDeleteAccountModal(): void {
    if (this.deletingAccount()) return;
    this.deleteAccountModalOpen.set(false);
  }

  protected confirmDeleteAccount(): void {
    if (this.deletingAccount()) return;

    this.deletingAccount.set(true);
    this.dangerError.set(null);
    this.preferencesService
      .deleteAccount()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.preferencesService.clearLocalPreferences();
          this.auth.clearLocalUser();
          this.router.navigate(['/login']);
        },
        error: (err: unknown) => {
          this.deletingAccount.set(false);
          this.dangerError.set(formatSettingsError(err));
        },
      });
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.deleteArchivedModalOpen()) {
      this.closeDeleteArchivedModal();
      return;
    }

    if (this.deleteAccountModalOpen()) {
      this.closeDeleteAccountModal();
    }
  }

  private loadCurrentUser(): void {
    const user = this.auth.user();
    if (user) {
      this.applyProfile(user.displayName, user.email);
      return;
    }

    this.auth
      .refreshCurrentUser()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (currentUser) => this.applyProfile(currentUser.displayName, currentUser.email),
        error: () => this.applyProfile('', ''),
      });
  }

  private loadPreferences(): void {
    this.preferencesLoading.set(true);
    this.preferencesError.set(null);
    this.preferencesService
      .loadPreferences()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (preferences) => {
          this.applyPreferencesToForm(preferences);
          this.preferencesLoading.set(false);
          this.preferencesDirty.set(false);
        },
        error: (err: unknown) => {
          this.preferencesLoading.set(false);
          this.preferencesError.set(formatSettingsError(err));
        },
      });
  }

  private loadCategories(): void {
    this.categoriesApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => this.categories.set(categories),
        error: () => this.categories.set([]),
      });
  }

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.dashboardApi
      .summary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (summary) => {
          this.summary.set(summary);
          this.summaryLoading.set(false);
        },
        error: () => {
          this.summary.set(null);
          this.summaryLoading.set(false);
        },
      });
  }

  private applyProfile(displayName: string, email: string): void {
    this.profileForm.patchValue({ displayName, email }, { emitEvent: false });
    this.profileDisplayName.set(displayName);
  }

  private applyPreferencesToForm(preferences: UserPreferences): void {
    this.applyingPreferences = true;
    this.settingsForm.reset(
      {
        theme: preferences.theme,
        density: preferences.density,
        defaultCategoryId: preferences.defaultCategoryId,
        autoExtractTitle: preferences.autoExtractTitle,
        showFavicon: preferences.showFavicon,
        openInNewTab: preferences.openInNewTab,
        confirmBeforeDelete: preferences.confirmBeforeDelete,
        tagsDefaultSort: preferences.tagsDefaultSort,
        categoriesDefaultSort: preferences.categoriesDefaultSort,
      },
      { emitEvent: false },
    );
    this.applyingPreferences = false;
  }

  // ── Import / Export ────────────────────────────────────────

  protected openSettingsImportModal(format: 'html' | 'json' = 'html'): void {
    this.settingsImportFormat.set(format);
    this.settingsImportModalOpen.set(true);
  }

  protected onSettingsImportClosed(_didImport: boolean): void {
    this.settingsImportModalOpen.set(false);
  }

  protected exportJson(): void {
    if (this.exportingJson()) return;
    this.exportingJson.set(true);
    this.linksApi.exportLinks('json').subscribe({
      next: (blob) => {
        triggerDownload(blob, 'favourites-export.json');
        this.exportingJson.set(false);
      },
      error: () => this.exportingJson.set(false),
    });
  }

  protected exportHtml(): void {
    if (this.exportingHtml()) return;
    this.exportingHtml.set(true);
    this.linksApi.exportLinks('html').subscribe({
      next: (blob) => {
        triggerDownload(blob, 'favourites-bookmarks.html');
        this.exportingHtml.set(false);
      },
      error: () => this.exportingHtml.set(false),
    });
  }

  private preferencesPayload(): PatchUserPreferencesRequest {
    const value = this.settingsForm.getRawValue();
    // Fields without UI controls (tag suggestions/colors have no feature
    // behind them yet; email notifications have no mail infrastructure) are
    // passed through unchanged from the stored preferences so the backend
    // contract stays intact.
    const stored = this.preferencesService.preferences();

    return {
      theme: value.theme,
      density: value.density,
      defaultCategoryId: value.defaultCategoryId || null,
      autoExtractTitle: value.autoExtractTitle,
      showFavicon: value.showFavicon,
      openInNewTab: value.openInNewTab,
      confirmBeforeDelete: value.confirmBeforeDelete,
      suggestTagsAutomatically: stored.suggestTagsAutomatically,
      showColorsOnTagChips: stored.showColorsOnTagChips,
      tagsDefaultSort: value.tagsDefaultSort,
      categoriesDefaultSort: value.categoriesDefaultSort,
      weeklySummaryEmail: stored.weeklySummaryEmail,
      securityAlerts: stored.securityAlerts,
      productUpdates: stored.productUpdates,
    };
  }
}

function triggerDownload(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

function formatSettingsError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) return 'Check the highlighted settings and try again.';
    if (status === 401) return 'Your session has expired. Sign in again to continue.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }

  return 'Something went wrong. Please try again.';
}
