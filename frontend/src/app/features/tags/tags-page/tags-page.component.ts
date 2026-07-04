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
import type { TagResponse, TagsSummaryResponse } from '../models/tag.models';
import { TagsApiService, type TagsSortOrder, type TagsStatusFilter } from '../services/tags-api.service';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import { ManageDuplicatesModalComponent } from '../manage-duplicates-modal/manage-duplicates-modal.component';
import { FocusTrapDirective } from '../../../shared/directives/focus-trap.directive';

const TILE_TONES: readonly TileAccentTone[] = [
  'purple', 'indigo', 'teal', 'sky', 'amber', 'rose', 'lime', 'green',
];

const SORT_OPTIONS = [
  { value: 'name', label: 'Name (A-Z)' },
  { value: 'most-used', label: 'Most used' },
  { value: 'least-used', label: 'Least used' },
  { value: 'newest', label: 'Newest' },
] as const;

type SortValue = TagsSortOrder;

const STATUS_OPTIONS = [
  { value: 'all', label: 'All tags' },
  { value: 'used', label: 'Used' },
  { value: 'unused', label: 'Unused' },
] as const;

type StatusValue = TagsStatusFilter;

const TAG_MENU_ITEMS: TileCardMenuItem[] = [
  { label: 'Rename' },
  { label: 'Merge', disabled: true },
  { label: 'Delete', danger: true },
];

const DEFAULT_PAGE_SIZE = 25;

@Component({
  selector: 'app-tags-page',
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
    ManageDuplicatesModalComponent,
    FocusTrapDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tags-page.component.html',
  styleUrl: './tags-page.component.scss',
})
export class TagsPageComponent implements OnInit {
  private readonly tagsApi = inject(TagsApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly viewModeService = inject(ViewModeService);
  private readonly preferencesService = inject(UserPreferencesService);

  protected readonly icons = FavIcons;
  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly statusOptions = STATUS_OPTIONS;

  // Tags list + pagination
  protected readonly tags = signal<TagResponse[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  // Summary / stat cards
  protected readonly summary = signal<TagsSummaryResponse | null>(null);
  protected readonly summaryLoading = signal(true);

  // Filters
  protected readonly searchControl = new FormControl<string>('', { nonNullable: true });
  protected readonly activeSearch = signal('');
  // Initial sort comes from Settings → Tags & categories → "Default tag sort".
  private readonly defaultSort: SortValue = this.preferencesService.preferences().tagsDefaultSort;
  protected readonly activeSort = signal<SortValue>(this.defaultSort);
  protected readonly activeStatus = signal<StatusValue>('all');
  protected readonly hasActiveFilters = computed(
    () =>
      this.activeSearch().length > 0 ||
      this.activeSort() !== this.defaultSort ||
      this.activeStatus() !== 'all',
  );

  // View mode
  protected viewMode = signal<ViewMode>(this.viewModeService.get('tags'));

  // Create tag
  protected readonly createControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, nonBlankValidator, Validators.maxLength(50)],
  });
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);
  protected readonly newTagModalOpen = signal(false);

  // Edit tag
  protected readonly editingTag = signal<TagResponse | null>(null);
  protected readonly editControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, nonBlankValidator, Validators.maxLength(50)],
  });
  protected readonly updating = signal(false);
  protected readonly updateError = signal<string | null>(null);

  // Delete tag
  protected readonly deleteTarget = signal<TagResponse | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  // Mobile filter sheet
  protected readonly filterSheetOpen = signal(false);

  // Manage duplicates modal
  protected readonly manageDuplicatesOpen = signal(false);

  // Popular tags for rail (top 5 most-used, from the current page)
  protected readonly popularTags = computed(() =>
    [...this.tags()]
      .sort((a, b) => b.linkCount - a.linkCount || a.name.localeCompare(b.name))
      .slice(0, 5),
  );

  protected readonly overloadedTags = computed(
    () => this.tags().filter((tag) => tag.linkCount >= 20).length,
  );

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

  // ── Manage duplicates modal ───────────────────────────────────────

  protected openManageDuplicates(): void {
    this.manageDuplicatesOpen.set(true);
  }

  protected onManageDuplicatesClosed(didMerge: boolean): void {
    this.manageDuplicatesOpen.set(false);
    if (didMerge) {
      this.load();
      this.loadSummary();
    }
  }

  // ── New tag modal ─────────────────────────────────────────────────────────

  protected openNewTagModal(): void {
    this.createControl.reset('');
    this.createError.set(null);
    this.newTagModalOpen.set(true);
  }

  protected closeNewTagModal(): void {
    if (this.creating()) return;
    this.newTagModalOpen.set(false);
  }

  protected createTag(): void {
    this.createError.set(null);
    if (this.createControl.invalid || this.creating()) {
      this.createControl.markAsTouched();
      return;
    }
    const name = this.createControl.value.trim();
    this.creating.set(true);
    this.tagsApi
      .create({ name })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.newTagModalOpen.set(false);
          this.page.set(1);
          this.load();
          this.loadSummary();
        },
        error: (err: unknown) => {
          this.creating.set(false);
          this.createError.set(formatTagMutationError(err, 'create'));
        },
      });
  }

  // ── Edit tag (rename) ─────────────────────────────────────────────────────

  protected startEditing(tag: TagResponse): void {
    if (this.updating() || this.deleting()) return;
    this.updateError.set(null);
    this.editingTag.set(tag);
    this.editControl.reset(tag.name);
  }

  protected cancelEditing(): void {
    if (this.updating()) return;
    this.editingTag.set(null);
    this.updateError.set(null);
  }

  protected saveEdit(): void {
    const tag = this.editingTag();
    if (!tag || this.updating()) return;
    this.updateError.set(null);
    if (this.editControl.invalid) {
      this.editControl.markAsTouched();
      return;
    }
    const name = this.editControl.value.trim();
    if (name === tag.name) {
      this.cancelEditing();
      return;
    }
    this.updating.set(true);
    this.tagsApi
      .update(tag.id, { name })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.updating.set(false);
          this.editingTag.set(null);
          this.tags.update((current) =>
            current.map((item) => (item.id === updated.id ? updated : item)),
          );
        },
        error: (err: unknown) => {
          this.updating.set(false);
          this.updateError.set(formatTagMutationError(err, 'update'));
        },
      });
  }

  // ── Delete tag ────────────────────────────────────────────────────────────

  protected openDeleteConfirm(tag: TagResponse): void {
    if (this.updating()) return;
    this.deleteError.set(null);
    this.deleteTarget.set(tag);
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
    this.tagsApi
      .remove(target.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deleting.set(false);
          this.deleteTarget.set(null);
          this.tags.update((current) => current.filter((t) => t.id !== target.id));
          this.total.update((n) => Math.max(0, n - 1));
          if (this.editingTag()?.id === target.id) this.editingTag.set(null);
          this.loadSummary();
        },
        error: (err: unknown) => {
          this.deleting.set(false);
          this.deleteError.set(formatDeleteTagError(err));
        },
      });
  }

  // ── Tile card 3-dot menu handler ──────────────────────────────────────────

  protected onTileMenuAction(tag: TagResponse, item: TileCardMenuItem): void {
    if (item.disabled) return;
    if (item.label === 'Rename') {
      this.startEditing(tag);
    } else if (item.label === 'Delete') {
      this.openDeleteConfirm(tag);
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

  protected getTileMenuItems(): TileCardMenuItem[] {
    return TAG_MENU_ITEMS;
  }

  protected linkCountLabel(count: number): string {
    return count === 1 ? '1 link' : `${count} links`;
  }

  protected lastUsedLabel(tag: TagResponse): string {
    if (!tag.lastUsedAtUtc) return 'Never used';
    return `Last used ${new Intl.DateTimeFormat(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    }).format(new Date(tag.lastUsedAtUtc))}`;
  }

  protected openTagLinks(tag: TagResponse): void {
    this.router.navigate(['/app/links'], { queryParams: { tagId: tag.id } });
  }

  protected trackById(_index: number, tag: TagResponse): string {
    return tag.id;
  }

  @HostListener('document:keydown.escape')
  protected onEscapeKey(): void {
    if (this.filterSheetOpen()) {
      this.closeFilterSheet();
      return;
    }
    if (this.deleteTarget()) {
      this.closeDeleteConfirm();
      return;
    }
    if (this.newTagModalOpen()) {
      this.closeNewTagModal();
      return;
    }
    if (this.editingTag()) {
      this.cancelEditing();
    }
  }

  // ── Private ───────────────────────────────────────────────────────────────

  private load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.tagsApi
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
          this.tags.set(paged.items);
          this.total.set(paged.total);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.loadError.set(formatLoadTagsError(err));
          this.loading.set(false);
        },
      });
  }

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.tagsApi
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
}

function formatLoadTagsError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to manage tags.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while loading tags. Please try again.';
}

function formatTagMutationError(err: unknown, action: 'create' | 'update'): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400)
      return apiValidationMessage(err) ?? 'Choose a unique tag name using 50 characters or fewer.';
    if (status === 401)
      return `Your session has expired. Sign in again to ${action} this tag.`;
    if (status === 404)
      return 'This tag is no longer available. It may have already been deleted.';
    if (status === 0)
      return 'Could not reach the server. Check your connection and try again.';
  }
  return `Something went wrong while trying to ${action} the tag. Please try again.`;
}

function formatDeleteTagError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to delete this tag.';
    if (status === 404) return 'This tag is no longer available. It may have already been deleted.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while deleting the tag. Please try again.';
}
