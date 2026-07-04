import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { FavIcons } from '../../../shared/icons/fav-icons';
import type { TagResponse } from '../../tags/models/tag.models';
import { TagsApiService } from '../../tags/services/tags-api.service';

let nextTagPickerId = 0;

/**
 * Tag picker for the add/edit link form.
 *
 * Renders as a chip-style typeahead so adding and removing tags is a
 * keyboard-first interaction:
 *
 *   - Type to filter — matching existing tags appear as suggestions.
 *   - Press Enter — if the typed value matches an existing tag, select it;
 *     otherwise create a new tag and select it.
 *   - Press Backspace on an empty input — remove the most recently
 *     selected tag.
 *   - Click a suggestion or a selected chip's × to mouse the same actions.
 *
 * Inputs / outputs are stable so callers
 * don't need to know about the keyboard ergonomics: `selectedTagIds` in,
 * `selectionChanged: string[]` out, `disabled` in.
 */
@Component({
  selector: 'app-link-tag-picker',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './link-tag-picker.component.html',
  styleUrl: './link-tag-picker.component.scss',
})
export class LinkTagPickerComponent implements OnInit {
  private readonly tagsApi = inject(TagsApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly idSuffix = ++nextTagPickerId;

  readonly selectedTagIds = input<readonly string[]>([]);
  readonly disabled = input<boolean>(false);

  readonly selectionChanged = output<string[]>();

  protected readonly icons = FavIcons;
  protected readonly inputId = `link-tag-picker-input-${this.idSuffix}`;
  protected readonly helpId = `link-tag-picker-help-${this.idSuffix}`;
  protected readonly inputErrorId = `link-tag-picker-error-${this.idSuffix}`;
  protected readonly suggestionsId = `link-tag-picker-suggestions-${this.idSuffix}`;

  @ViewChild('tagInput') private readonly tagInputRef?: ElementRef<HTMLInputElement>;

  protected readonly tags = signal<TagResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly creating = signal(false);
  protected readonly inputError = signal<string | null>(null);
  protected readonly inputValue = signal<string>('');

  protected readonly selectedTags = computed(() => {
    const selected = this.selectedTagIds();
    return selected
      .map((id) => this.tags().find((tag) => tag.id === id))
      .filter((tag): tag is TagResponse => tag !== undefined);
  });

  /** Tags matching the current input that are not already selected. */
  protected readonly suggestions = computed(() => {
    const query = this.inputValue().trim().toLowerCase();
    const selected = new Set(this.selectedTagIds());
    return this.tags()
      .filter((tag) => !selected.has(tag.id))
      .filter((tag) => query.length === 0 || tag.name.toLowerCase().includes(query))
      .slice(0, 8);
  });

  /** True when the typed value would create a new tag on Enter. */
  protected readonly canCreateFromInput = computed(() => {
    const query = this.inputValue().trim();
    if (query.length === 0 || query.length > 50) return false;
    const lowered = query.toLowerCase();
    return !this.tags().some((tag) => tag.name.toLowerCase() === lowered);
  });

  ngOnInit(): void {
    this.loadTags();
  }

  protected reload(): void {
    this.loadTags();
  }

  protected onInput(event: Event): void {
    this.inputError.set(null);
    const input = event.target as HTMLInputElement;
    this.inputValue.set(input.value);
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (this.disabled() || this.creating()) return;
    const value = this.inputValue();

    if (event.key === 'Enter') {
      event.preventDefault();
      this.commitInput();
      return;
    }

    if (event.key === 'Backspace' && value.length === 0) {
      const selected = this.selectedTagIds();
      if (selected.length > 0) {
        event.preventDefault();
        this.emitSelection(selected.slice(0, -1));
      }
    }
  }

  protected selectSuggestion(tag: TagResponse): void {
    if (this.disabled() || this.creating()) return;
    this.emitSelection([...this.selectedTagIds(), tag.id]);
    this.resetInput();
    this.focusInput();
  }

  protected removeTag(tagId: string): void {
    if (this.disabled() || this.creating()) return;
    this.emitSelection(this.selectedTagIds().filter((id) => id !== tagId));
    this.focusInput();
  }

  protected focusInputOnContainerClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.focusInput();
    }
  }

  protected trackById(_index: number, tag: TagResponse): string {
    return tag.id;
  }

  private commitInput(): void {
    const trimmed = this.inputValue().trim();
    if (trimmed.length === 0) return;
    if (trimmed.length > 50) {
      this.inputError.set('Tag name must be 50 characters or fewer.');
      return;
    }
    const lowered = trimmed.toLowerCase();
    const existing = this.tags().find((tag) => tag.name.toLowerCase() === lowered);
    if (existing) {
      if (!this.selectedTagIds().includes(existing.id)) {
        this.emitSelection([...this.selectedTagIds(), existing.id]);
      }
      this.resetInput();
      return;
    }
    this.createTag(trimmed);
  }

  private createTag(name: string): void {
    this.creating.set(true);
    this.tagsApi
      .create({ name })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (tag) => {
          this.creating.set(false);
          this.tags.update((current) => sortTags([...current, tag]));
          this.emitSelection([...this.selectedTagIds(), tag.id]);
          this.resetInput();
          this.focusInput();
        },
        error: (err: unknown) => {
          this.creating.set(false);
          this.inputError.set(formatCreateTagError(err));
        },
      });
  }

  private resetInput(): void {
    this.inputValue.set('');
    if (this.tagInputRef) {
      this.tagInputRef.nativeElement.value = '';
    }
  }

  private focusInput(): void {
    queueMicrotask(() => this.tagInputRef?.nativeElement.focus());
  }

  private loadTags(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.tagsApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (tags) => {
          this.tags.set(sortTags(tags));
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.loadError.set(formatLoadTagsError(err));
          this.loading.set(false);
        },
      });
  }

  private emitSelection(tagIds: readonly string[]): void {
    const unique = [...new Set(tagIds)];
    this.selectionChanged.emit(unique);
  }
}

function sortTags(tags: readonly TagResponse[]): TagResponse[] {
  return [...tags].sort((a, b) => a.name.localeCompare(b.name));
}

function formatLoadTagsError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) {
      return 'Your session has expired. Sign in again to choose tags.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong while loading tags. Please try again.';
}

function formatCreateTagError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) {
      return 'That tag could not be created. It may already exist.';
    }
    if (status === 401) {
      return 'Your session has expired. Sign in again to create tags.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong while creating the tag. Please try again.';
}
