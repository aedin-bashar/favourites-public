import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  type AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  type ValidationErrors,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { FavIcons } from '../../../shared/icons/fav-icons';
import { apiValidationMessage } from '../../../shared/validation/api-validation-errors';
import { nonBlankValidator } from '../../../shared/validation/non-blank.validator';
import { AuthLayoutComponent } from '../auth-layout/auth-layout.component';

/**
 * Register page — display name, email, password, confirm password.
 * Source: docs/UI_DESIGN_GUIDE.md §12, §19 (Register page).
 *
 * Backend password rules live in `Infrastructure/DependencyInjection.cs`;
 * client-side validation enforces only basic length so that the server
 * remains the authority on policy. A server validation failure surfaces
 * inline via the `serverError` signal.
 *
 * On success the cookie is already set by the server and the user is
 * navigated to /app.
 */
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AuthLayoutComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly icons = FavIcons;

  protected readonly form = new FormGroup(
    {
      displayName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, nonBlankValidator, Validators.maxLength(64)],
      }),
      email: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, nonBlankValidator, Validators.email],
      }),
      password: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, nonBlankValidator, Validators.minLength(12)],
      }),
      confirmPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, nonBlankValidator],
      }),
    },
    { validators: passwordsMatchValidator },
  );

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly showPassword = signal(false);
  protected readonly showConfirmPassword = signal(false);

  protected onSubmit(): void {
    this.serverError.set(null);
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = this.form.getRawValue();
    this.submitting.set(true);

    this.auth
      .register(payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.router.navigateByUrl('/app');
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.serverError.set(formatRegisterError(err));
        },
      });
  }
}

function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value as string | undefined;
  const confirm = group.get('confirmPassword')?.value as string | undefined;
  if (!password || !confirm) return null;
  return password === confirm ? null : { passwordsMismatch: true };
}

function formatRegisterError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) {
      return apiValidationMessage(err) ?? 'Check the highlighted fields and try again.';
    }
    if (status === 409) {
      return 'An account with this email already exists.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong while creating your account. Please try again.';
}
