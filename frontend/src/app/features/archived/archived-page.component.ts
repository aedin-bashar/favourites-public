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
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { debounceTime, distinctUntilChanged, map } from 'rxjs/operators';

import { FavIcons } from '../../shared/icons/fav-icons';
import { UserPreferencesService } from '../settings/services/user-preferences.service';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import { RailWidgetComponent } from '../../shared/components/right-rail/rail-widget.component';
import { RightRailComponent } from '../../shared/components/right-rail/right-rail.component';
import { ViewToggleComponent } from '../../shared/components/view-toggle/view-toggle.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusPillComponent } from '../../shared/components/status-pill/status-pill.component';
import { ViewModeService, type ViewMode } from '../../shared/components/view-toggle/view-mode.service';
import { LinkCardComponent } from '../links/link-card/link-card.component';
import { DomainExtractPipe } from '../links/link-list/domain-extract.pipe';
import { FaviconUrlPipe } from '../../shared/pipes/favicon-url.pipe';
import { FocusTrapDirective } from '../../shared/directives/focus-trap.directive';
import type { CategoryResponse } from '../categories/models/category.models';
import { CategoriesApiService } from '../categories/services/categories-api.service';
import type { TagResponse } from '../tags/models/tag.models';
import { TagsApiService } from '../tags/services/tags-api.service';
import type { LinkResponse } from '../links/models/link.models';
import {
  LinksApiService,
  type LinksSortOrder,
  type PagedLinksResponse,
} from '../links/services/links-api.service';
import type { ArchivedSummaryResponse } from './models/archived.models';
import { ArchivedApiService } from './services/archived-api.service';

export type ArchivedSortOrder = 'recently-archived' | 'oldest-archived' | 'title';
export type ArchivedDateRange = 'anytime' | 'last7' | 'last30' | 'last90';

const SORT_OPTIONS: readonly { value: ArchivedSortOrder; label: string }[] = [
  { value: 'recently-archived', label: 'Recently archived' },
  { value: 'oldest-archived', label: 'Oldest archived' },
  { value: 'title', label: 'Title (A-Z)' },
];

const DATE_RANGE_OPTIONS: readonly { value: ArchivedDateRange; label: string }[] = [
  { value: 'anytime', label: 'Anytime' },
  { value: 'last7', label: 'Last 7 days' },
  { value: 'last30', label: 'Last 30 days' },
  { value: 'last90', label: 'Last 90 days' },
];

const BANNER_DISMISSED_KEY = 'fav_archived_banner_dismissed';
const DEFAULT_PAGE_SIZE = 25;

@Component({
  selector: 'app-archived-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    DomainExtractPipe,
    FaviconUrlPipe,
    StatCardComponent,
    RailWidgetComponent,
    RightRailComponent,
    ViewToggleComponent,
    PaginationComponent,
    StatusPillComponent,
    LinkCardComponent,
    FocusTrapDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './archived-page.component.html',
  styleUrl: './archived-page.component.scss',
})
export class ArchivedPageComponent implements OnInit {
  private readonly archivedApi = inject(ArchivedApiService);
  private readonly linksApi = inject(LinksApiService);
  private readonly tagsApi = inject(TagsApiService);
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly viewModeService = inject(ViewModeService);

  protected readonly icons = FavIcons;
  // Display/behaviour preferences from Settings (favicons, open target,
  // delete confirmation).
  protected readonly preferences = inject(UserPreferencesService).preferences;
  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly dateRangeOptions = DATE_RANGE_OPTIONS;

  // ── Summary / stat cards ──────────────────────────────────────
  protected readonly summary = signal<ArchivedSummaryResponse | null>(null);
  protected readonly summaryLoading = signal(true);

  // ── Archived links (paginated) ────────────────────────────────────────
  protected readonly links = signal<LinkResponse[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly displayedLinks = computed(() => this.links());

  // ── Info banner ────────────────────────────────────────────────
  protected readonly bannerDismissed = signal(
    localStorage.getItem(BANNER_DISMISSED_KEY) === 'true',
  );

  // ── Filters ───────────────────────────────────────────────────────────
  protected readonly searchControl = new FormControl<string>('', { nonNullable: true });
  protected readonly activeSearch = signal('');
  protected readonly activeCategoryId = signal<string | null>(null);
  protected readonly activeTagId = signal<string | null>(null);
  protected readonly activeDateRange = signal<ArchivedDateRange>('anytime');
  protected readonly activeSort = signal<ArchivedSortOrder>('recently-archived');

  protected readonly hasActiveFilters = computed(
    () =>
      this.activeSearch().length > 0 ||
      this.activeCategoryId() !== null ||
      this.activeTagId() !== null ||
      this.activeDateRange() !== 'anytime' ||
      this.activeSort() !== 'recently-archived',
  );

  // ── View mode ─────────────────────────────────────────────────────────
  protected viewMode = signal<ViewMode>(this.viewModeService.get('archived'));

  // ── Filter dropdown data ──────────────────────────────────────────────
  protected readonly tags = signal<TagResponse[]>([]);
  protected readonly categories = signal<CategoryResponse[]>([]);

  // ── Multi-select ───────────────────────────────────────────────
  protected readonly selectedIds = signal<Set<string>>(new Set());
  protected readonly hasSelection = computed(() => this.selectedIds().size > 0);
  protected readonly allSelected = computed(
    () =>
      this.displayedLinks().length > 0 &&
      this.displayedLinks().every((l) => this.selectedIds().has(l.id)),
  );

  // ── Single link actions ───────────────────────────────────────────────
  protected readonly restoreBusyId = signal<string | null>(null);
  protected readonly restoreError = signal<string | null>(null);
  protected readonly deleteTarget = signal<LinkResponse | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  // ── Bulk actions ──────────────────────────────────────────────
  protected readonly bulkRestoring = signal(false);
  protected readonly bulkRestoreError = signal<string | null>(null);
  protected readonly emptyingArchive = signal(false);
  protected readonly emptyArchiveError = signal<string | null>(null);

  // ── Mobile filter sheet ───────────────────────────────────────────────
  protected readonly filterSheetOpen = signal(false);

  // ── Cleanup suggestions — dedicated endpoint ────────────────
  protected readonly cleanupSuggestions = signal<LinkResponse[]>([]);
  protected readonly cleanupLoading = signal(false);
  protected readonly deletingSuggestionId = signal<string | null>(null);

  // ── Confirm dialogs ───────────────────────────────────────────
  protected readonly emptyArchiveModalOpen = signal(false);
  protected readonly restoreManyModalOpen = signal(false);

  private previousFocusedElement: HTMLElement | null = null;

  ngOnInit(): void {
    this.searchControl.valueChanges
      .pipe(
        map((v) => v.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((value) => {
        this.activeSearch.set(value);
        this.page.set(1);
        this.load();
      });

    this.loadSummary();
    this.loadFilters();
    this.load();
    this.loadCleanupSuggestions();
  }

  // ── Data loading ──────────────────────────────────────────────────────

  protected reload(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.linksApi
      .listPaged({
        search: this.activeSearch() || null,
        tagId: this.activeTagId(),
        categoryId: this.activeCategoryId(),
        sort: mapSortOrder(this.activeSort()),
        archived: 'archived',
        archivedFrom: archivedRangeStartIso(this.activeDateRange()),
        page: this.page(),
        pageSize: this.pageSize(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result: PagedLinksResponse) => {
          this.links.set(result.items);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(formatLoadError(err));
          this.loading.set(false);
        },
      });
  }

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.archivedApi
      .summary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.summary.set(data);
          this.summaryLoading.set(false);
        },
        error: () => {
          this.summaryLoading.set(false);
        },
      });
  }

  private loadCleanupSuggestions(): void {
    this.cleanupLoading.set(true);
    this.linksApi
      .cleanupSuggestions()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.cleanupSuggestions.set(items);
          this.cleanupLoading.set(false);
        },
        error: () => this.cleanupLoading.set(false),
      });
  }

  protected deleteSuggestion(link: LinkResponse): void {
    if (this.deletingSuggestionId()) return;
    this.deletingSuggestionId.set(link.id);
    this.linksApi.remove(link.id).subscribe({
      next: () => {
        this.cleanupSuggestions.update((items) => items.filter((i) => i.id !== link.id));
        this.deletingSuggestionId.set(null);
        // Refresh summary counts so stat cards stay accurate.
        this.loadSummary();
      },
      error: () => this.deletingSuggestionId.set(null),
    });
  }

  private loadFilters(): void {
    this.tagsApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (tags) => this.tags.set(tags) });

    this.categoriesApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (cats) => this.categories.set(cats) });
  }

  // ── Banner ────────────────────────────────────────────────────────────

  protected dismissBanner(): void {
    localStorage.setItem(BANNER_DISMISSED_KEY, 'true');
    this.bannerDismissed.set(true);
  }

  // ── Filter handlers ───────────────────────────────────────────────────

  protected clearSearch(): void {
    this.searchControl.setValue('');
    this.activeSearch.set('');
  }

  protected onCategoryChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.activeCategoryId.set(value || null);
    this.page.set(1);
    this.load();
  }

  protected onTagChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.activeTagId.set(value || null);
    this.page.set(1);
    this.load();
  }

  protected onDateRangeChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as ArchivedDateRange;
    this.activeDateRange.set(value);
    this.page.set(1);
    this.load();
  }

  protected onSortChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as ArchivedSortOrder;
    this.activeSort.set(value);
    this.page.set(1);
    this.load();
  }

  protected clearAllFilters(): void {
    this.searchControl.setValue('');
    this.activeSearch.set('');
    this.activeCategoryId.set(null);
    this.activeTagId.set(null);
    this.activeDateRange.set('anytime');
    this.activeSort.set('recently-archived');
    this.page.set(1);
    this.load();
  }

  // ── View mode ─────────────────────────────────────────────────────────

  protected onViewModeChange(mode: ViewMode): void {
    this.viewMode.set(mode);
  }

  // ── Pagination ────────────────────────────────────────────────────────

  protected onPageChange(newPage: number): void {
    this.page.set(newPage);
    this.selectedIds.set(new Set());
    this.load();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  // ── Multi-select ──────────────────────────────────────────────────────

  protected toggleSelect(linkId: string): void {
    this.selectedIds.update((current) => {
      const next = new Set(current);
      if (next.has(linkId)) {
        next.delete(linkId);
      } else {
        next.add(linkId);
      }
      return next;
    });
  }

  protected toggleSelectAll(): void {
    if (this.allSelected()) {
      this.selectedIds.set(new Set());
    } else {
      this.selectedIds.set(new Set(this.displayedLinks().map((l) => l.id)));
    }
  }

  // ── Single link actions ───────────────────────────────────────────────

  protected onRestoreRequested(link: LinkResponse): void {
    if (this.restoreBusyId() !== null) return;
    this.restoreError.set(null);
    this.restoreBusyId.set(link.id);
    this.linksApi
      .restore(link.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.restoreBusyId.set(null);
          this.selectedIds.update((s) => {
            const next = new Set(s);
            next.delete(link.id);
            return next;
          });
          this.load();
          this.loadSummary();
        },
        error: (err: unknown) => {
          this.restoreBusyId.set(null);
          this.restoreError.set(formatRestoreError(err));
        },
      });
  }

  protected onDeleteRequested(link: LinkResponse): void {
    this.deleteError.set(null);
    // "Confirm before delete" preference (Settings → Link defaults): when
    // switched off, delete straight away without the confirmation modal.
    if (!this.preferences().confirmBeforeDelete) {
      this.performDelete(link);
      return;
    }
    this.rememberFocusedElement();
    this.deleteTarget.set(link);
  }

  protected closeDeleteConfirm(): void {
    if (this.deleting()) return;
    this.deleteTarget.set(null);
    this.restoreFocusedElement();
  }

  protected confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target || this.deleting()) return;
    this.performDelete(target);
  }

  private performDelete(target: LinkResponse): void {
    if (this.deleting()) return;

    this.deleting.set(true);
    this.deleteError.set(null);
    this.linksApi
      .remove(target.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deleting.set(false);
          if (this.deleteTarget()) {
            this.deleteTarget.set(null);
            this.restoreFocusedElement();
          }
          this.links.update((current) => current.filter((l) => l.id !== target.id));
          this.total.update((t) => Math.max(0, t - 1));
          this.selectedIds.update((s) => {
            const next = new Set(s);
            next.delete(target.id);
            return next;
          });
          this.loadSummary();
        },
        error: (err: unknown) => {
          this.deleting.set(false);
          this.deleteError.set(formatDeleteError(err));
        },
      });
  }

  protected onEditRequested(link: LinkResponse): void {
    this.router.navigate(['/app/links', link.id]);
  }

  // ── Bulk: restore selected ────────────────────────────────────

  protected openRestoreManyModal(): void {
    this.rememberFocusedElement();
    this.restoreManyModalOpen.set(true);
  }

  protected closeRestoreManyModal(): void {
    if (this.bulkRestoring()) return;
    this.restoreManyModalOpen.set(false);
    this.restoreFocusedElement();
  }

  protected confirmRestoreMany(): void {
    const ids = [...this.selectedIds()];
    if (ids.length === 0 || this.bulkRestoring()) return;

    this.bulkRestoring.set(true);
    this.bulkRestoreError.set(null);
    this.archivedApi
      .restoreMany(ids)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.bulkRestoring.set(false);
          this.restoreManyModalOpen.set(false);
          this.selectedIds.set(new Set());
          this.load();
          this.loadSummary();
          this.restoreFocusedElement();
        },
        error: (err: unknown) => {
          this.bulkRestoring.set(false);
          this.bulkRestoreError.set(formatBulkError(err));
        },
      });
  }

  // ── Bulk: empty archive ───────────────────────────────────────

  protected openEmptyArchiveModal(): void {
    this.rememberFocusedElement();
    this.emptyArchiveModalOpen.set(true);
  }

  protected closeEmptyArchiveModal(): void {
    if (this.emptyingArchive()) return;
    this.emptyArchiveModalOpen.set(false);
    this.restoreFocusedElement();
  }

  protected confirmEmptyArchive(): void {
    if (this.emptyingArchive()) return;
    this.emptyingArchive.set(true);
    this.emptyArchiveError.set(null);
    this.archivedApi
      .deleteAllArchived()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.emptyingArchive.set(false);
          this.emptyArchiveModalOpen.set(false);
          this.selectedIds.set(new Set());
          this.load();
          this.loadSummary();
          this.restoreFocusedElement();
        },
        error: (err: unknown) => {
          this.emptyingArchive.set(false);
          this.emptyArchiveError.set(formatBulkError(err));
        },
      });
  }

  // ── Mobile filter sheet ───────────────────────────────────────────────

  protected openFilterSheet(): void {
    this.filterSheetOpen.set(true);
  }

  protected closeFilterSheet(): void {
    this.filterSheetOpen.set(false);
  }

  protected applyFiltersFromSheet(): void {
    this.filterSheetOpen.set(false);
    this.page.set(1);
    this.load();
  }

  // ── Keyboard shortcuts ────────────────────────────────────────────────

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.filterSheetOpen()) { this.closeFilterSheet(); return; }
    if (this.deleteTarget()) { this.closeDeleteConfirm(); return; }
    if (this.restoreManyModalOpen()) { this.closeRestoreManyModal(); return; }
    if (this.emptyArchiveModalOpen()) { this.closeEmptyArchiveModal(); }
  }

  // ── Track by ─────────────────────────────────────────────────────────

  protected trackById(_index: number, link: LinkResponse): string {
    return link.id;
  }

  // ── Focus helpers ─────────────────────────────────────────────────────

  private rememberFocusedElement(): void {
    const active = document.activeElement;
    this.previousFocusedElement = active instanceof HTMLElement ? active : null;
  }

  private restoreFocusedElement(): void {
    const element = this.previousFocusedElement;
    this.previousFocusedElement = null;
    queueMicrotask(() => element?.focus());
  }
}

// Maps the archived page's sort enum to the links API sort enum
function mapSortOrder(sort: ArchivedSortOrder): LinksSortOrder {
  switch (sort) {
    case 'recently-archived': return 'recently-updated';
    case 'oldest-archived': return 'oldest-archived';
    case 'title': return 'title';
  }
}

function archivedRangeStartIso(range: ArchivedDateRange): string | null {
  if (range === 'anytime') return null;
  const now = new Date();
  switch (range) {
    case 'last7': return new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString();
    case 'last30': return new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString();
    case 'last90': return new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000).toISOString();
  }
}

function formatLoadError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to view archived links.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while loading archived links. Please try again.';
}

function formatRestoreError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to restore this link.';
    if (status === 404) return 'This link is no longer available.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while restoring the link. Please try again.';
}

function formatDeleteError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to delete this link.';
    if (status === 404) return 'This link is no longer available. It may have already been deleted.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while deleting the link. Please try again.';
}

function formatBulkError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Please sign in again.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong. Please try again.';
}

