import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { FavIcons } from '../../../shared/icons/fav-icons';
import { apiValidationMessage } from '../../../shared/validation/api-validation-errors';
import { nonBlankValidator } from '../../../shared/validation/non-blank.validator';
import { AuthLayoutComponent } from '../auth-layout/auth-layout.component';

/**
 * Login page — email + password, Remember-me, Forgot-password link, and a
 * link to the register page. Source: docs/UI_DESIGN_GUIDE.md §12, §19.
 *
 * On success, navigates to the `returnUrl` query parameter (set by
 * `authGuard`) or to `/app` as the default landing zone.
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AuthLayoutComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly icons = FavIcons;

  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, nonBlankValidator, Validators.email],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, nonBlankValidator],
    }),
    rememberMe: new FormControl(false, { nonNullable: true }),
  });

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected onSubmit(): void {
    this.serverError.set(null);
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password, rememberMe } = this.form.getRawValue();
    this.submitting.set(true);

    this.auth
      .login({ email, password, rememberMe })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
          this.router.navigateByUrl(returnUrl ?? '/app');
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.serverError.set(formatLoginError(err));
        },
      });
  }
}

function formatLoginError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) {
      return apiValidationMessage(err) ?? 'Enter your email and password to sign in.';
    }
    if (status === 401) {
      return 'Email or password is incorrect.';
    }
    if (status === 0) {
      return 'Could not reach the server. Check your connection and try again.';
    }
  }
  return 'Something went wrong while signing in. Please try again.';
}
