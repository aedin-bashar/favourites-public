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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { FavIcons } from '../../../shared/icons/fav-icons';
import { nonBlankValidator } from '../../../shared/validation/non-blank.validator';
import { AuthLayoutComponent } from '../auth-layout/auth-layout.component';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AuthLayoutComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reset-password.component.html',
})
export class ResetPasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly icons = FavIcons;
  protected readonly showPassword = signal(false);
  protected readonly showConfirmPassword = signal(false);

  protected readonly form = new FormGroup(
    {
      newPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, nonBlankValidator, Validators.minLength(12)],
      }),
      confirmNewPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, nonBlankValidator],
      }),
    },
    { validators: passwordsMatchValidator },
  );

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  private get token(): string {
    return this.route.snapshot.queryParamMap.get('token') ?? '';
  }

  protected onSubmit(): void {
    this.serverError.set(null);
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    if (!this.token) {
      this.serverError.set('The reset link is missing a token. Request a new reset link.');
      return;
    }

    const { newPassword, confirmNewPassword } = this.form.getRawValue();
    this.submitting.set(true);

    this.auth
      .resetPassword({ token: this.token, newPassword, confirmNewPassword })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.router.navigateByUrl('/login');
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.serverError.set(formatResetError(err));
        },
      });
  }
}

function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const pw = group.get('newPassword')?.value as string | undefined;
  const confirm = group.get('confirmNewPassword')?.value as string | undefined;
  if (!pw || !confirm) return null;
  return pw === confirm ? null : { passwordsMismatch: true };
}

function formatResetError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) {
      return 'The reset link is invalid or has expired. Request a new one.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong. Please try again.';
}
