import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  type AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  type ValidationErrors,
  Validators,
} from '@angular/forms';

import { FavIcons } from '../../../shared/icons/fav-icons';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import { apiValidationMessage } from '../../../shared/validation/api-validation-errors';
import { nonBlankValidator } from '../../../shared/validation/non-blank.validator';
import { LinkCategoryPickerComponent } from '../link-category-picker/link-category-picker.component';
import { LinkTagPickerComponent } from '../link-tag-picker/link-tag-picker.component';
import type { CreateLinkRequest, LinkResponse } from '../models/link.models';
import { LinksApiService } from '../services/links-api.service';
import { deriveTitleFromUrl } from '../utils/derive-title-from-url';

/**
 * Add-link form.
 *
 * Self-contained reusable form that collects URL, title, and (optional)
 * description and POSTs to `/api/links` via {@link LinksApiService}.
 *
 * Paste-URL-fast path:
 *   - The URL field auto-focuses when the form mounts so the keyboard caret
 *     lands in the only required-to-paste field.
 *   - When the URL field changes (paste/blur), if the title is still empty
 *     and untouched we suggest a title derived from the URL. This lets the
 *     user save with a single paste + click; they can edit the title
 *     afterwards if they want something more descriptive.
 *
 * Outputs:
 *   - `linkCreated` — emits the persisted `LinkResponse` on success.
 *   - `cancelled` — emits when the user clicks Cancel.
 */
@Component({
  selector: 'app-add-link-form',
  standalone: true,
  imports: [ReactiveFormsModule, LinkTagPickerComponent, LinkCategoryPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './add-link-form.component.html',
  styleUrl: './add-link-form.component.scss',
})
export class AddLinkFormComponent implements AfterViewInit {
  private readonly linksApi = inject(LinksApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly preferencesService = inject(UserPreferencesService);

  /** When true the form renders a Cancel button beside Save. */
  readonly showCancel = input<boolean>(false);

  readonly linkCreated = output<LinkResponse>();
  readonly cancelled = output<void>();

  protected readonly icons = FavIcons;

  @ViewChild('urlInput') private readonly urlInputRef?: ElementRef<HTMLInputElement>;

  protected readonly form = new FormGroup({
    url: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        nonBlankValidator,
        Validators.maxLength(2048),
        httpUrlValidator,
      ],
    }),
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, nonBlankValidator, Validators.maxLength(200)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(2000)],
    }),
  });

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly selectedTagIds = signal<string[]>([]);
  // "Default category" link default from Settings pre-selects the category;
  // the user can still clear or change it before saving.
  protected readonly selectedCategoryId = signal<string | null>(
    this.preferencesService.preferences().defaultCategoryId,
  );

  /** True while we're auto-filling the title from the URL. */
  private titleAutoFilled = false;

  ngAfterViewInit(): void {
    // Auto-focus the URL field so paste-and-save is the fastest path.
    queueMicrotask(() => this.urlInputRef?.nativeElement.focus());
  }

  protected onUrlInput(): void {
    // "Suggest title from URL" link default from Settings gates the suggestion.
    if (!this.preferencesService.preferences().autoExtractTitle) return;
    const url = this.form.controls.url.value;
    const titleControl = this.form.controls.title;
    // Only suggest a title while the user hasn't typed their own. We treat
    // an auto-filled title as still-fair-game to overwrite while the URL
    // keeps changing.
    if (titleControl.dirty && !this.titleAutoFilled) {
      return;
    }
    const derived = deriveTitleFromUrl(url);
    if (derived) {
      titleControl.setValue(derived, { emitEvent: false });
      this.titleAutoFilled = true;
    }
  }

  protected onTitleInput(): void {
    // Once the user edits the title themselves, stop overwriting it.
    this.titleAutoFilled = false;
  }

  protected onSubmit(): void {
    this.serverError.set(null);
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const description = raw.description.trim();
    const payload: CreateLinkRequest = {
      url: raw.url.trim(),
      title: raw.title.trim(),
      description: description.length > 0 ? description : null,
      tagIds: this.selectedTagIds(),
      categoryId: this.selectedCategoryId(),
    };

    this.submitting.set(true);
    this.linksApi
      .create(payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (link) => {
          this.submitting.set(false);
          this.form.reset();
          this.titleAutoFilled = false;
          this.selectedTagIds.set([]);
          this.selectedCategoryId.set(this.preferencesService.preferences().defaultCategoryId);
          this.linkCreated.emit(link);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.serverError.set(formatCreateLinkError(err));
        },
      });
  }

  protected onCancel(): void {
    if (this.submitting()) return;
    this.cancelled.emit();
  }

  protected onTagSelectionChanged(tagIds: string[]): void {
    this.selectedTagIds.set(tagIds);
  }

  protected onCategorySelectionChanged(categoryId: string | null): void {
    this.selectedCategoryId.set(categoryId);
  }
}

function httpUrlValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value as string | null | undefined)?.trim();
  if (!value) return null;
  let parsed: URL;
  try {
    parsed = new URL(value);
  } catch {
    return { url: true };
  }
  return parsed.protocol === 'http:' || parsed.protocol === 'https:'
    ? null
    : { url: true };
}

function formatCreateLinkError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) {
      return apiValidationMessage(err) ?? 'Check the highlighted link fields and try again.';
    }
    if (status === 401) {
      return 'Your session has expired. Sign in again to save this link.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong while saving the link. Please try again.';
}
