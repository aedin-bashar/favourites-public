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
import { ActivatedRoute, Router } from '@angular/router';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, map } from 'rxjs/operators';

import type { CategoryResponse } from '../../categories/models/category.models';
import { CategoriesApiService } from '../../categories/services/categories-api.service';
import type { TagResponse } from '../../tags/models/tag.models';
import { TagsApiService } from '../../tags/services/tags-api.service';
import { FavIcons } from '../../../shared/icons/fav-icons';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import { AddLinkFormComponent } from '../add-link-form/add-link-form.component';
import { ImportBookmarksModalComponent } from '../import-bookmarks-modal/import-bookmarks-modal.component';
import { LinkCardComponent } from '../link-card/link-card.component';
import type { LinkResponse } from '../models/link.models';
import {
  LinksApiService,
  type LinksArchivedFilter,
  type LinksSortOrder,
  type PagedLinksResponse,
} from '../services/links-api.service';
import { RailWidgetComponent } from '../../../shared/components/right-rail/rail-widget.component';
import { StatusPillComponent } from '../../../shared/components/status-pill/status-pill.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { ViewToggleComponent } from '../../../shared/components/view-toggle/view-toggle.component';
import { ViewModeService, type ViewMode } from '../../../shared/components/view-toggle/view-mode.service';
import { DatePipe } from '@angular/common';
import { DomainExtractPipe } from './domain-extract.pipe';
import { FaviconUrlPipe } from '../../../shared/pipes/favicon-url.pipe';
import { FocusTrapDirective } from '../../../shared/directives/focus-trap.directive';

const DEFAULT_SORT: LinksSortOrder = 'newest';
const DEFAULT_ARCHIVED: LinksArchivedFilter = 'active';
const DEFAULT_PAGE_SIZE = 25;

interface SortOption {
  readonly value: LinksSortOrder;
  readonly label: string;
}

const SORT_OPTIONS: readonly SortOption[] = [
  { value: 'newest', label: 'Newest first' },
  { value: 'oldest', label: 'Oldest first' },
  { value: 'title', label: 'Title (A-Z)' },
  { value: 'recently-updated', label: 'Recently updated' },
];

@Component({
  selector: 'app-link-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    DomainExtractPipe,
    FaviconUrlPipe,
    AddLinkFormComponent,
    ImportBookmarksModalComponent,
    LinkCardComponent,
    RailWidgetComponent,
    StatusPillComponent,
    PaginationComponent,
    ViewToggleComponent,
    FocusTrapDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './link-list.component.html',
  styleUrl: './link-list.component.scss',
})
export class LinkListComponent implements OnInit {
  private readonly linksApi = inject(LinksApiService);
  private readonly tagsApi = inject(TagsApiService);
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly viewModeService = inject(ViewModeService);

  protected readonly icons = FavIcons;
  // Display/behaviour preferences from Settings (favicons, open target,
  // delete confirmation).
  protected readonly preferences = inject(UserPreferencesService).preferences;

  protected readonly links = signal<LinkResponse[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly searchControl = new FormControl<string>('', { nonNullable: true });
  protected readonly activeSearch = signal<string>('');
  protected readonly hasActiveSearch = computed(() => this.activeSearch().length > 0);

  protected readonly tags = signal<TagResponse[]>([]);
  protected readonly categories = signal<CategoryResponse[]>([]);
  protected readonly tagsLoading = signal(true);
  protected readonly categoriesLoading = signal(true);
  protected readonly tagsError = signal<string | null>(null);
  protected readonly categoriesError = signal<string | null>(null);
  protected readonly activeTagId = signal<string | null>(null);
  protected readonly activeCategoryId = signal<string | null>(null);
  protected readonly activeStatus = signal<LinksArchivedFilter>(DEFAULT_ARCHIVED);
  protected readonly pageTitle = computed(() =>
    this.activeStatus() === 'archived' ? 'Archived Links' : 'All Links',
  );
  protected readonly emptyTitle = computed(() => {
    if (
      this.activeStatus() === 'archived' &&
      !this.hasActiveSearch() &&
      this.activeTagId() === null &&
      this.activeCategoryId() === null
    ) {
      return 'No archived links';
    }
    if (this.hasActiveSearch()) return `No links match "${this.activeSearch()}"`;
    if (this.hasActiveFilters()) return 'No links match the current filters';
    return 'No links to show yet';
  });
  protected readonly hasActiveFilters = computed(
    () =>
      this.hasActiveSearch() ||
      this.activeTagId() !== null ||
      this.activeCategoryId() !== null ||
      this.activeStatus() !== DEFAULT_ARCHIVED,
  );

  protected readonly deleteTarget = signal<LinkResponse | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly addLinkOpen = signal(false);

  protected readonly sortOrder = signal<LinksSortOrder>(DEFAULT_SORT);
  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly archiveBusyId = signal<string | null>(null);
  protected readonly archiveError = signal<string | null>(null);

  // View mode (list | cards)
  protected viewMode = signal<ViewMode>(this.viewModeService.get('links'));

  // Mobile filter sheet
  protected readonly filterSheetOpen = signal(false);

  // Import bookmarks modal
  protected readonly importModalOpen = signal(false);

  private previousFocusedElement: HTMLElement | null = null;

  ngOnInit(): void {
    // React to query-param changes so that the global search bar (which
    // navigates to /app/links?search=…) triggers a reload even when the
    // component is already mounted on this route.
    let firstEmission = true;
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((qp) => {
        const search = qp.get('search')?.trim() ?? '';
        const tagId = qp.get('tagId') ?? null;
        const categoryId = qp.get('categoryId') ?? null;
        const sort = (qp.get('sort') as LinksSortOrder | null) ?? DEFAULT_SORT;
        const status = (qp.get('status') as LinksArchivedFilter | null) ?? DEFAULT_ARCHIVED;
        const page = Math.max(1, parseInt(qp.get('page') ?? '1', 10) || 1);

        // Detect whether anything actually changed relative to current state.
        // When the in-page controls call syncUrl(), queryParamMap fires again
        // with values identical to what the signals already hold — skip the
        // redundant load in that case.
        // Always load on first emission so fresh navigation to /app/links
        // triggers a load even when all params match the signal defaults.
        const unchanged =
          !firstEmission &&
          search === this.activeSearch() &&
          tagId === this.activeTagId() &&
          categoryId === this.activeCategoryId() &&
          sort === this.sortOrder() &&
          status === this.activeStatus() &&
          page === this.page();
        firstEmission = false;

        if (unchanged) return;

        // Apply all params to signals.
        if (search !== this.searchControl.value) {
          this.searchControl.setValue(search, { emitEvent: false });
        }
        this.activeSearch.set(search);
        this.activeTagId.set(tagId);
        this.activeCategoryId.set(categoryId);
        if (SORT_OPTIONS.some((o) => o.value === sort)) this.sortOrder.set(sort);
        this.activeStatus.set(status);
        this.page.set(page);

        if (qp.has('add')) this.addLinkOpen.set(true);

        this.load();
      });

    this.searchControl.valueChanges
      .pipe(
        map((value) => value.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((value) => {
        this.activeSearch.set(value);
        this.page.set(1);
        this.syncUrl();
        this.load();
      });

    this.loadFilters();
  }

  protected reload(): void {
    this.load();
  }

  protected reloadFilters(): void {
    this.loadFilters();
  }

  protected openImportModal(): void {
    this.rememberFocusedElement();
    this.importModalOpen.set(true);
  }

  protected onImportClosed(didImport: boolean): void {
    this.importModalOpen.set(false);
    this.restoreFocusedElement();
    if (didImport) {
      this.page.set(1);
      this.load();
      this.loadFilters();
    }
  }

  protected openAddLink(): void {
    this.rememberFocusedElement();
    this.addLinkOpen.set(true);
  }

  protected closeAddLink(): void {
    this.addLinkOpen.set(false);
    this.restoreFocusedElement();
  }

  protected onLinkCreated(_link: LinkResponse): void {
    this.addLinkOpen.set(false);
    this.page.set(1);
    this.load();
  }

  protected clearSearch(): void {
    if (!this.hasActiveSearch() && !this.searchControl.value) return;
    // Use emitEvent:false to avoid the valueChanges pipe, which would be
    // blocked by distinctUntilChanged when the control was last set silently
    // (e.g. populated from a URL query param with emitEvent:false).
    this.searchControl.setValue('', { emitEvent: false });
    this.activeSearch.set('');
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  protected onTagFilterChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.activeTagId.set(select.value === '' ? null : select.value);
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  protected onCategoryFilterChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.activeCategoryId.set(select.value === '' ? null : select.value);
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  protected onSortChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const next =
      SORT_OPTIONS.find((option) => option.value === select.value)?.value ?? DEFAULT_SORT;
    this.sortOrder.set(next);
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  protected onStatusFilterChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.setStatusFilter((select.value as LinksArchivedFilter) || DEFAULT_ARCHIVED);
  }

  protected setStatusFilter(status: LinksArchivedFilter): void {
    this.activeStatus.set(status);
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  protected onViewModeChange(mode: ViewMode): void {
    this.viewMode.set(mode);
  }

  protected clearAllFilters(): void {
    this.searchControl.setValue('');
    this.activeSearch.set('');
    this.activeTagId.set(null);
    this.activeCategoryId.set(null);
    this.activeStatus.set(DEFAULT_ARCHIVED);
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  protected onPageChange(newPage: number): void {
    this.page.set(newPage);
    this.syncUrl();
    this.load();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  // Quick filter shortcuts from right rail
  protected applyQuickFilter(filter: 'newest' | 'most-used' | 'archived'): void {
    if (filter === 'archived') {
      this.activeStatus.set('archived');
    } else if (filter === 'newest') {
      this.sortOrder.set('newest');
      this.activeStatus.set('active');
    } else if (filter === 'most-used') {
      this.sortOrder.set('recently-updated');
      this.activeStatus.set('active');
    }
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  // Filter by tag from rail widget chip
  protected filterByTag(tagId: string): void {
    this.activeTagId.set(tagId);
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  // Filter by category from rail widget
  protected filterByCategory(categoryId: string): void {
    this.activeCategoryId.set(categoryId);
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  // Mobile filter sheet
  protected openFilterSheet(): void {
    this.filterSheetOpen.set(true);
  }

  protected closeFilterSheet(): void {
    this.filterSheetOpen.set(false);
  }

  protected applyFiltersFromSheet(): void {
    this.filterSheetOpen.set(false);
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  protected trackById(_index: number, link: LinkResponse): string {
    return link.id;
  }

  protected trackTagById(_index: number, tag: TagResponse): string {
    return tag.id;
  }

  protected trackCategoryById(_index: number, category: CategoryResponse): string {
    return category.id;
  }

  protected onLinkOpened(_link: LinkResponse): void {}

  protected onEditRequested(link: LinkResponse): void {
    this.router.navigate(['/app/links', link.id]);
  }

  protected onArchiveRequested(link: LinkResponse): void {
    if (this.archiveBusyId() !== null) return;

    this.archiveError.set(null);
    this.archiveBusyId.set(link.id);
    this.linksApi
      .archive(link.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.archiveBusyId.set(null);
          this.load();
        },
        error: (err: unknown) => {
          this.archiveBusyId.set(null);
          this.archiveError.set(formatArchiveError(err, 'archive'));
        },
      });
  }

  protected onRestoreRequested(link: LinkResponse): void {
    if (this.archiveBusyId() !== null) return;

    this.archiveError.set(null);
    this.archiveBusyId.set(link.id);
    this.linksApi
      .restore(link.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.archiveBusyId.set(null);
          this.load();
        },
        error: (err: unknown) => {
          this.archiveBusyId.set(null);
          this.archiveError.set(formatArchiveError(err, 'restore'));
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
          this.links.update((current) => current.filter((link) => link.id !== target.id));
          this.total.update((t) => Math.max(0, t - 1));
        },
        error: (err: unknown) => {
          this.deleting.set(false);
          this.deleteError.set(formatDeleteError(err));
        },
      });
  }

  @HostListener('document:keydown.escape', ['$event'])
  protected onEscapeKey(event: Event): void {
    if (event.defaultPrevented) return;

    if (this.filterSheetOpen()) {
      this.closeFilterSheet();
      return;
    }

    if (this.deleteTarget()) {
      this.closeDeleteConfirm();
      return;
    }

    if (this.addLinkOpen()) {
      this.closeAddLink();
    }
  }

  // Load paginated results and update total
  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.linksApi
      .listPaged({
        search: this.activeSearch(),
        tagId: this.activeTagId(),
        categoryId: this.activeCategoryId(),
        sort: this.sortOrder(),
        archived: this.activeStatus(),
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
          this.error.set(formatListError(err));
          this.loading.set(false);
        },
      });
  }

  private loadFilters(): void {
    this.tagsLoading.set(true);
    this.tagsError.set(null);
    this.tagsApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (tags) => {
          this.tags.set([...tags].sort((a, b) => a.name.localeCompare(b.name)));
          this.tagsLoading.set(false);
        },
        error: (err: unknown) => {
          this.tags.set([]);
          this.tagsError.set(formatFilterTagsError(err));
          this.tagsLoading.set(false);
        },
      });

    this.categoriesLoading.set(true);
    this.categoriesError.set(null);
    this.categoriesApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => {
          this.categories.set([...categories].sort((a, b) => a.name.localeCompare(b.name)));
          this.categoriesLoading.set(false);
        },
        error: (err: unknown) => {
          this.categories.set([]);
          this.categoriesError.set(formatFilterCategoriesError(err));
          this.categoriesLoading.set(false);
        },
      });
  }

  private syncUrl(): void {
    const qp: Record<string, string> = {};
    const search = this.activeSearch();
    if (search) qp['search'] = search;
    const tag = this.activeTagId();
    if (tag) qp['tagId'] = tag;
    const cat = this.activeCategoryId();
    if (cat) qp['categoryId'] = cat;
    if (this.sortOrder() !== DEFAULT_SORT) qp['sort'] = this.sortOrder();
    if (this.activeStatus() !== DEFAULT_ARCHIVED) qp['status'] = this.activeStatus();
    if (this.page() > 1) qp['page'] = String(this.page());

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: qp,
      replaceUrl: true,
    });
  }

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

function formatListError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to view your links.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while loading your links. Please try again.';
}

function formatFilterTagsError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Sign in again to load tag filters.';
    if (status === 0) return 'Could not reach the server to load tag filters.';
  }
  return 'Tag filters are unavailable right now.';
}

function formatFilterCategoriesError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Sign in again to load category filters.';
    if (status === 0) return 'Could not reach the server to load category filters.';
  }
  return 'Category filters are unavailable right now.';
}

function formatArchiveError(err: unknown, action: 'archive' | 'restore'): string {
  const verb = action === 'archive' ? 'archive' : 'restore';
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return `Your session has expired. Sign in again to ${verb} this link.`;
    if (status === 404) return 'This link is no longer available. It may have been removed.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return `Something went wrong while trying to ${verb} the link. Please try again.`;
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
