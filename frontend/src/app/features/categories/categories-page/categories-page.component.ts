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
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, map } from 'rxjs/operators';

import { FavIcons } from '../../../shared/icons/fav-icons';
import { apiValidationMessage } from '../../../shared/validation/api-validation-errors';
import { nonBlankValidator } from '../../../shared/validation/non-blank.validator';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { RailWidgetComponent } from '../../../shared/components/right-rail/rail-widget.component';
import { RightRailComponent } from '../../../shared/components/right-rail/right-rail.component';
import { ViewToggleComponent } from '../../../shared/components/view-toggle/view-toggle.component';
import { ViewModeService, type ViewMode } from '../../../shared/components/view-toggle/view-mode.service';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { TileCardComponent } from '../../../shared/components/tile-card/tile-card.component';
import type { TileAccentTone, TileCardMenuItem } from '../../../shared/components/tile-card/tile-card.component';
import type { CategoriesSummaryResponse, CategoryResponse } from '../models/category.models';
import {
  CategoriesApiService,
  type CategoriesSortOrder,
  type CategoriesStatusFilter,
} from '../services/categories-api.service';
import { OrganizeCategoriesModalComponent } from '../organize-categories-modal/organize-categories-modal.component';
import { FocusTrapDirective } from '../../../shared/directives/focus-trap.directive';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';

const TILE_TONES: readonly TileAccentTone[] = [
  'purple', 'indigo', 'teal', 'sky', 'amber', 'rose', 'lime', 'green',
];

const SORT_OPTIONS = [
  { value: 'name', label: 'Name (A-Z)' },
  { value: 'largest', label: 'Largest' },
  { value: 'recently-active', label: 'Recently active' },
  { value: 'newest', label: 'Newest' },
] as const;

type SortValue = CategoriesSortOrder;

const STATUS_OPTIONS = [
  { value: 'all', label: 'All' },
  { value: 'used', label: 'Used' },
  { value: 'empty', label: 'Empty' },
] as const;

type StatusValue = CategoriesStatusFilter;

const CATEGORY_MENU_ITEMS: TileCardMenuItem[] = [
  { label: 'Rename' },
  { label: 'Delete', danger: true },
];

const DEFAULT_PAGE_SIZE = 25;

@Component({
  selector: 'app-categories-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    StatCardComponent,
    RailWidgetComponent,
    RightRailComponent,
    ViewToggleComponent,
    PaginationComponent,
    TileCardComponent,
    OrganizeCategoriesModalComponent,
    FocusTrapDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './categories-page.component.html',
  styleUrl: './categories-page.component.scss',
})
export class CategoriesPageComponent implements OnInit {
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly viewModeService = inject(ViewModeService);
  private readonly preferencesService = inject(UserPreferencesService);

  protected readonly icons = FavIcons;
  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly statusOptions = STATUS_OPTIONS;

  // Current page of categories
  protected readonly categories = signal<CategoryResponse[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  // Summary / stat cards
  protected readonly summary = signal<CategoriesSummaryResponse | null>(null);
  protected readonly summaryLoading = signal(true);

  protected readonly totalCategories = computed(() => this.summary()?.totalCategories ?? this.total());
  protected readonly emptyCategories = computed(() => this.summary()?.emptyCategories ?? 0);

  // Top categories for right rail (current page, sorted by link count)
  protected readonly topCategories = computed(() =>
    [...this.categories()]
      .sort((a, b) => b.linkCount - a.linkCount || a.name.localeCompare(b.name))
      .slice(0, 5),
  );

  // Filters
  protected readonly searchControl = new FormControl<string>('', { nonNullable: true });
  protected readonly activeSearch = signal('');
  // Initial sort comes from Settings → Tags & categories → "Default category sort".
  private readonly defaultSort: SortValue =
    this.preferencesService.preferences().categoriesDefaultSort;
  protected readonly activeSort = signal<SortValue>(this.defaultSort);
  protected readonly activeStatus = signal<StatusValue>('all');
  protected readonly hasActiveFilters = computed(
    () =>
      this.activeSearch().length > 0 ||
      this.activeSort() !== this.defaultSort ||
      this.activeStatus() !== 'all',
  );

  // View mode
  protected viewMode = signal<ViewMode>(this.viewModeService.get('categories'));

  // Create category
  protected readonly createControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, nonBlankValidator, Validators.maxLength(50)],
  });
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);
  protected readonly newCategoryModalOpen = signal(false);

  // Edit category
  protected readonly editingCategory = signal<CategoryResponse | null>(null);
  protected readonly editControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, nonBlankValidator, Validators.maxLength(50)],
  });
  protected readonly editColor = signal<string>('#0d6efd');
  protected readonly updating = signal(false);
  protected readonly updateError = signal<string | null>(null);

  protected readonly colorPalette = [
    '#0d6efd', '#6610f2', '#d63384', '#dc3545',
    '#fd7e14', '#198754', '#0dcaf0', '#6f42c1',
  ];

  // Delete category
  protected readonly deleteTarget = signal<CategoryResponse | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  // Mobile filter sheet
  protected readonly filterSheetOpen = signal(false);

  // Organize categories modal
  protected readonly organizeCategoriesOpen = signal(false);

  protected readonly uncategorizedLinks = computed(() => this.summary()?.uncategorizedLinks ?? 0);

  protected largestBarWidth(index: number): number {
    const categories = this.topCategories();
    const max = Math.max(...categories.map((category) => category.linkCount), 0);
    const category = categories[index];
    if (!category || max === 0) return 0;
    return Math.max(12, Math.round((category.linkCount / max) * 100));
  }

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
    this.load();
  }

  protected reload(): void {
    this.load();
    this.loadSummary();
  }

  // ── View mode ─────────────────────────────────────────────────────────────

  protected onViewModeChange(mode: ViewMode): void {
    this.viewMode.set(mode);
  }

  // ── Filters ───────────────────────────────────────────────────────────────

  protected onSortChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as SortValue;
    this.activeSort.set(value);
    this.page.set(1);
    this.load();
  }

  protected onStatusChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as StatusValue;
    this.activeStatus.set(value);
    this.page.set(1);
    this.load();
  }

  protected clearSearch(): void {
    this.searchControl.setValue('');
  }

  protected clearAllFilters(): void {
    this.searchControl.setValue('');
    this.activeSearch.set('');
    this.activeSort.set(this.defaultSort);
    this.activeStatus.set('all');
    this.page.set(1);
    this.load();
  }

  // ── Pagination ────────────────────────────────────────────────────────────

  protected onPageChange(newPage: number): void {
    this.page.set(newPage);
    this.load();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  // ── New category modal ────────────────────────────────────────────────────

  protected openOrganizeCategories(): void {
    this.organizeCategoriesOpen.set(true);
  }

  protected onOrganizeCategoriesClosed(didMerge: boolean): void {
    this.organizeCategoriesOpen.set(false);
    if (didMerge) {
      this.load();
      this.loadSummary();
    }
  }

  protected openNewCategoryModal(): void {
    this.createControl.reset('');
    this.createError.set(null);
    this.newCategoryModalOpen.set(true);
  }

  protected closeNewCategoryModal(): void {
    if (this.creating()) return;
    this.newCategoryModalOpen.set(false);
  }

  protected createCategory(): void {
    this.createError.set(null);
    if (this.createControl.invalid || this.creating()) {
      this.createControl.markAsTouched();
      return;
    }
    const name = this.createControl.value.trim();
    this.creating.set(true);
    this.categoriesApi
      .create({ name })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.newCategoryModalOpen.set(false);
          this.page.set(1);
          this.load();
          this.loadSummary();
        },
        error: (err: unknown) => {
          this.creating.set(false);
          this.createError.set(formatCategoryMutationError(err, 'create'));
        },
      });
  }

  // ── Edit category (rename) ─────────────────────────────────────────────────

  protected startEditing(category: CategoryResponse): void {
    if (this.updating() || this.deleting()) return;
    this.updateError.set(null);
    this.editingCategory.set(category);
    this.editControl.reset(category.name);
    this.editColor.set(category.color);
  }

  protected onCustomColorInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (value) this.editColor.set(value.toLowerCase());
  }

  protected cancelEditing(): void {
    if (this.updating()) return;
    this.editingCategory.set(null);
    this.updateError.set(null);
  }

  protected saveEdit(): void {
    const category = this.editingCategory();
    if (!category || this.updating()) return;
    this.updateError.set(null);
    if (this.editControl.invalid) {
      this.editControl.markAsTouched();
      return;
    }
    const name = this.editControl.value.trim();
    const color = this.editColor();
    if (name === category.name && color === category.color) {
      this.cancelEditing();
      return;
    }
    this.updating.set(true);
    this.categoriesApi
      .update(category.id, { name, color })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.updating.set(false);
          this.editingCategory.set(null);
          this.categories.update((current) =>
            current.map((item) => (item.id === updated.id ? updated : item)),
          );
        },
        error: (err: unknown) => {
          this.updating.set(false);
          this.updateError.set(formatCategoryMutationError(err, 'update'));
        },
      });
  }

  // ── Delete category ───────────────────────────────────────────────────────

  protected openDeleteConfirm(category: CategoryResponse): void {
    if (this.updating()) return;
    this.deleteError.set(null);
    this.deleteTarget.set(category);
  }

  protected closeDeleteConfirm(): void {
    if (this.deleting()) return;
    this.deleteTarget.set(null);
  }

  protected confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target || this.deleting()) return;
    this.deleting.set(true);
    this.deleteError.set(null);
    this.categoriesApi
      .remove(target.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deleting.set(false);
          this.deleteTarget.set(null);
          if (this.editingCategory()?.id === target.id) this.editingCategory.set(null);
          this.load();
          this.loadSummary();
        },
        error: (err: unknown) => {
          this.deleting.set(false);
          this.deleteError.set(formatDeleteCategoryError(err));
        },
      });
  }

  // ── Tile card 3-dot menu ──────────────────────────────────────────────────

  protected onTileMenuAction(category: CategoryResponse, item: TileCardMenuItem): void {
    if (item.disabled) return;
    if (item.label === 'Rename') {
      this.startEditing(category);
    } else if (item.label === 'Delete') {
      this.openDeleteConfirm(category);
    }
  }

  // ── Mobile filter sheet ───────────────────────────────────────────────────

  protected openFilterSheet(): void {
    this.filterSheetOpen.set(true);
  }

  protected closeFilterSheet(): void {
    this.filterSheetOpen.set(false);
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  protected toneForIndex(index: number): TileAccentTone {
    return TILE_TONES[index % TILE_TONES.length];
  }

  protected getCategoryMenuItems(): TileCardMenuItem[] {
    return CATEGORY_MENU_ITEMS;
  }

  protected linkCountLabel(count: number): string {
    return count === 1 ? '1 link' : `${count} links`;
  }

  protected activityLabel(category: CategoryResponse): string {
    if (!category.lastActivityAtUtc) return 'No activity yet';
    return `Active ${new Intl.DateTimeFormat(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    }).format(new Date(category.lastActivityAtUtc))}`;
  }

  protected openCategoryLinks(category: CategoryResponse): void {
    this.router.navigate(['/app/links'], { queryParams: { categoryId: category.id } });
  }

  protected trackById(_index: number, category: CategoryResponse): string {
    return category.id;
  }

  @HostListener('document:keydown.escape')
  protected onEscapeKey(): void {
    if (this.filterSheetOpen()) { this.closeFilterSheet(); return; }
    if (this.deleteTarget()) { this.closeDeleteConfirm(); return; }
    if (this.newCategoryModalOpen()) { this.closeNewCategoryModal(); return; }
    if (this.editingCategory()) { this.cancelEditing(); }
  }

  // ── Private ───────────────────────────────────────────────────────────────

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.categoriesApi
      .summary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (s) => {
          this.summary.set(s);
          this.summaryLoading.set(false);
        },
        error: () => {
          this.summaryLoading.set(false);
        },
      });
  }

  private load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.categoriesApi
      .list({
        page: this.page(),
        pageSize: this.pageSize(),
        search: this.activeSearch() || null,
        status: this.activeStatus(),
        sort: this.activeSort(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (paged) => {
          this.categories.set(paged.items);
          this.total.set(paged.total);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.loadError.set(formatLoadCategoriesError(err));
          this.loading.set(false);
        },
      });
  }
}

function formatLoadCategoriesError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to manage categories.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while loading categories. Please try again.';
}

function formatCategoryMutationError(err: unknown, action: 'create' | 'update'): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400)
      return apiValidationMessage(err) ?? 'Choose a unique category name using 50 characters or fewer.';
    if (status === 401)
      return `Your session has expired. Sign in again to ${action} this category.`;
    if (status === 404)
      return 'This category is no longer available. It may have already been deleted.';
    if (status === 0)
      return 'Could not reach the server. Check your connection and try again.';
  }
  return `Something went wrong while trying to ${action} the category. Please try again.`;
}

function formatDeleteCategoryError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to delete this category.';
    if (status === 404) return 'This category is no longer available. It may have already been deleted.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while deleting the category. Please try again.';
}
