import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterOutlet } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { UserPreferencesService } from '../settings/services/user-preferences.service';
import { AppShellComponent } from '../../shared/layouts/app-shell/app-shell.component';
import { MoreSheetComponent } from '../../shared/layouts/app-shell/more-sheet.component';

/**
 * Authenticated app layout route. Renders AppShellComponent and projects the
 * active child route via <router-outlet>. The mobile More sheet is managed here
 * so that navigation actions (Add, Sign out) have access to the Router and
 * AuthService.
 */
@Component({
  selector: 'app-app-layout',
  standalone: true,
  imports: [RouterOutlet, AppShellComponent, MoreSheetComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app-layout.component.html',
  styleUrl: './app-layout.component.scss',
})
export class AppLayoutComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly preferencesService = inject(UserPreferencesService);

  constructor() {
    // Load the user's saved preferences once per authenticated session so
    // theme/density and behavioural preferences apply on every page, not
    // just after visiting Settings.
    this.preferencesService
      .loadPreferences()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  protected readonly displayName = computed(() => this.auth.user()?.displayName ?? null);
  protected readonly email = computed(() => this.auth.user()?.email ?? null);
  protected readonly moreOpen = signal(false);

  protected onSignOut(): void {
    this.moreOpen.set(false);
    this.auth
      .logout()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.router.navigateByUrl('/login'),
        error: () => {
          this.auth.clearLocalUser();
          this.router.navigateByUrl('/login');
        },
      });
  }

  protected onAddClick(): void {
    this.moreOpen.set(false);
    this.router.navigate(['/app/links'], { queryParams: { add: 1 } });
  }

  protected onMoreClick(): void {
    this.moreOpen.update((v) => !v);
  }

  protected closeMore(): void {
    this.moreOpen.set(false);
  }
}
