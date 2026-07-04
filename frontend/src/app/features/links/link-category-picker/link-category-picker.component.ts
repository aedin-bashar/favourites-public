import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import type { CategoryResponse } from '../../categories/models/category.models';
import { CategoriesApiService } from '../../categories/services/categories-api.service';
import { FavIcons } from '../../../shared/icons/fav-icons';

let nextCategoryPickerId = 0;

/**
 * Category picker for the add/edit link form.
 *
 * Single-select combobox: the input shows the current category's name (or is
 * empty for "no category"). Type to filter, Enter to pick the matching
 * category or create a new one. Esc / × clears the selection.
 *
 * Inputs / outputs:
 *   - `selectedCategoryId: string | null` in
 *   - `selectionChanged: string | null` out
 *   - `disabled` in
 */
@Component({
  selector: 'app-link-category-picker',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './link-category-picker.component.html',
  styleUrl: './link-category-picker.component.scss',
})
export class LinkCategoryPickerComponent implements OnInit {
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly idSuffix = ++nextCategoryPickerId;

  readonly selectedCategoryId = input<string | null>(null);
  readonly disabled = input<boolean>(false);

  readonly selectionChanged = output<string | null>();

  protected readonly icons = FavIcons;
  protected readonly inputId = `link-category-picker-input-${this.idSuffix}`;
  protected readonly helpId = `link-category-picker-help-${this.idSuffix}`;
  protected readonly inputErrorId = `link-category-picker-error-${this.idSuffix}`;
  protected readonly suggestionsId = `link-category-picker-suggestions-${this.idSuffix}`;

  @ViewChild('categoryInput') private readonly categoryInputRef?: ElementRef<HTMLInputElement>;

  protected readonly categories = signal<CategoryResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly creating = signal(false);
  protected readonly inputError = signal<string | null>(null);

  /**
   * Current input contents. Initialised from the resolved selected category
   * name once categories have loaded so the field shows the user what is
   * currently picked. `null` means "uninitialised — adopt the selection's
   * name as soon as it can be resolved".
   */
  protected readonly inputValue = signal<string | null>(null);

  protected readonly selectedCategory = computed(() => {
    const id = this.selectedCategoryId();
    if (!id) return null;
    return this.categories().find((category) => category.id === id) ?? null;
  });

  protected readonly displayValue = computed(() => {
    const explicit = this.inputValue();
    if (explicit !== null) return explicit;
    return this.selectedCategory()?.name ?? '';
  });

  protected readonly suggestions = computed(() => {
    const query = (this.inputValue() ?? '').trim().toLowerCase();
    return this.categories()
      .filter((category) => query.length === 0 || category.name.toLowerCase().includes(query))
      .slice(0, 8);
  });

  protected readonly canCreateFromInput = computed(() => {
    const query = (this.inputValue() ?? '').trim();
    if (query.length === 0 || query.length > 50) return false;
    const lowered = query.toLowerCase();
    return !this.categories().some((category) => category.name.toLowerCase() === lowered);
  });

  constructor() {
    // Whenever the selected id changes (e.g. parent reset on submit, or
    // edit-form re-seed), drop any local in-progress input so the field
    // shows the selection's name again.
    effect(() => {
      this.selectedCategoryId();
      this.inputValue.set(null);
      this.inputError.set(null);
    });
  }

  ngOnInit(): void {
    this.loadCategories();
  }

  protected reload(): void {
    this.loadCategories();
  }

  protected onInput(event: Event): void {
    this.inputError.set(null);
    const input = event.target as HTMLInputElement;
    this.inputValue.set(input.value);
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (this.disabled() || this.creating()) return;

    if (event.key === 'Enter') {
      event.preventDefault();
      this.commitInput();
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.inputValue.set(null);
      this.inputError.set(null);
    }
  }

  protected onBlur(): void {
    const current = this.inputValue();
    if (current === null) return;
    if (current.trim().length === 0) {
      // Treat clearing the field via blur as "no category".
      if (this.selectedCategoryId() !== null) {
        this.selectionChanged.emit(null);
      }
      this.inputValue.set(null);
      return;
    }
    // Restore the displayed name to the actual selection on blur so the
    // field doesn't lie about what's persisted.
    this.inputValue.set(null);
  }

  protected selectSuggestion(category: CategoryResponse): void {
    if (this.disabled() || this.creating()) return;
    this.selectionChanged.emit(category.id);
    this.inputValue.set(null);
    this.focusInput();
  }

  protected clearSelection(): void {
    if (this.disabled() || this.creating()) return;
    this.selectionChanged.emit(null);
    this.inputValue.set(null);
    this.focusInput();
  }

  protected focusInputOnContainerClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.focusInput();
    }
  }

  protected trackById(_index: number, category: CategoryResponse): string {
    return category.id;
  }

  private commitInput(): void {
    const trimmed = (this.inputValue() ?? '').trim();
    if (trimmed.length === 0) {
      // Empty Enter clears the selection.
      this.clearSelection();
      return;
    }
    if (trimmed.length > 50) {
      this.inputError.set('Category name must be 50 characters or fewer.');
      return;
    }
    const lowered = trimmed.toLowerCase();
    const existing = this.categories().find(
      (category) => category.name.toLowerCase() === lowered,
    );
    if (existing) {
      this.selectionChanged.emit(existing.id);
      this.inputValue.set(null);
      return;
    }
    this.createCategory(trimmed);
  }

  private createCategory(name: string): void {
    this.creating.set(true);
    this.categoriesApi
      .create({ name })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (category) => {
          this.creating.set(false);
          this.categories.update((current) => sortCategories([...current, category]));
          this.selectionChanged.emit(category.id);
          this.inputValue.set(null);
          this.focusInput();
        },
        error: (err: unknown) => {
          this.creating.set(false);
          this.inputError.set(formatCreateCategoryError(err));
        },
      });
  }

  private focusInput(): void {
    queueMicrotask(() => this.categoryInputRef?.nativeElement.focus());
  }

  private loadCategories(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.categoriesApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => {
          this.categories.set(sortCategories(categories));
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.loadError.set(formatLoadCategoriesError(err));
          this.loading.set(false);
        },
      });
  }
}

function sortCategories(categories: readonly CategoryResponse[]): CategoryResponse[] {
  return [...categories].sort((a, b) => a.name.localeCompare(b.name));
}

function formatLoadCategoriesError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) {
      return 'Your session has expired. Sign in again to choose a category.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong while loading categories. Please try again.';
}

function formatCreateCategoryError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) {
      return 'That category could not be created. It may already exist.';
    }
    if (status === 401) {
      return 'Your session has expired. Sign in again to create categories.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong while creating the category. Please try again.';
}
