import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
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
import { apiValidationMessage } from '../../../shared/validation/api-validation-errors';
import { nonBlankValidator } from '../../../shared/validation/non-blank.validator';
import { LinkCategoryPickerComponent } from '../link-category-picker/link-category-picker.component';
import { LinkTagPickerComponent } from '../link-tag-picker/link-tag-picker.component';
import type {
  LinkResponse,
  UpdateLinkRequest,
} from '../models/link.models';
import { LinksApiService } from '../services/links-api.service';

/**
 * Edit-link form.
 *
 * Self-contained reusable form that loads an existing
 * {@link LinkResponse} into URL / title / description fields and PUTs
 * the result to `/api/links/{id}` via {@link LinksApiService.update}.
 * Mirrors the add-link form's validators (`UpdateFavouriteLinkCommandValidator`
 * on the backend); the server stays the authority and a 400 surfaces inline.
 *
 * Outputs:
 *   - `linkUpdated` — emits the persisted `LinkResponse` on success so
 *     the parent can refresh the details view, close a modal, etc.
 *   - `cancelled` — emits when the user clicks Cancel. The parent owns
 *     dismissal (close modal, navigate back, …).
 *
 * The `link` input is required and re-populates the form whenever it
 * changes (e.g. when reopening the edit view for a different link).
 */
@Component({
  selector: 'app-edit-link-form',
  standalone: true,
  imports: [ReactiveFormsModule, LinkTagPickerComponent, LinkCategoryPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './edit-link-form.component.html',
  styleUrl: './edit-link-form.component.scss',
})
export class EditLinkFormComponent {
  private readonly linksApi = inject(LinksApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly link = input.required<LinkResponse>();

  readonly linkUpdated = output<LinkResponse>();
  readonly cancelled = output<void>();

  protected readonly icons = FavIcons;

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
  protected readonly selectedCategoryId = signal<string | null>(null);

  constructor() {
    effect(() => {
      const current = this.link();
      this.form.reset({
        url: current.url,
        title: current.title,
        description: current.description ?? '',
      });
      this.selectedTagIds.set(current.tags.map((tag) => tag.id));
      this.selectedCategoryId.set(current.category?.id ?? null);
      this.serverError.set(null);
    });
  }

  protected onSubmit(): void {
    this.serverError.set(null);
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const description = raw.description.trim();
    const payload: UpdateLinkRequest = {
      url: raw.url.trim(),
      title: raw.title.trim(),
      description: description.length > 0 ? description : null,
      tagIds: this.selectedTagIds(),
      categoryId: this.selectedCategoryId(),
    };

    this.submitting.set(true);
    this.linksApi
      .update(this.link().id, payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.submitting.set(false);
          this.linkUpdated.emit(updated);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.serverError.set(formatUpdateLinkError(err));
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

function formatUpdateLinkError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) {
      return apiValidationMessage(err) ?? 'Check the highlighted link fields and try again.';
    }
    if (status === 401) {
      return 'Your session has expired. Sign in again to update this link.';
    }
    if (status === 404) {
      return 'This link is no longer available. It may have been deleted.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong while updating the link. Please try again.';
}
