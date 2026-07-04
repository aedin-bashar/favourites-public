import { inject } from '@angular/core';
import { Router, type CanActivateFn, type UrlTree } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Authentication guard for routes that require a signed-in user.
 *
 * If the AuthService already holds a user, the route is allowed immediately.
 * Otherwise the guard probes `/api/auth/current-user` so a still-valid
 * session cookie can re-hydrate the user. If that probe fails (401, etc.)
 * the user is redirected to /login with a `returnUrl` query parameter so
 * they can be sent back after a successful sign-in.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return auth.refreshCurrentUser().pipe(
    map((): boolean | UrlTree => true),
    catchError((): Observable<boolean | UrlTree> =>
      of(router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })),
    ),
  );
};

/**
 * Inverse of {@link authGuard} — for routes like /login and /register that
 * should NOT be visible once the user is already signed in. Redirects
 * authenticated visitors to /app.
 *
 * When no user is held in memory (e.g. a fresh browser start), the guard
 * probes `/api/auth/current-user` first so a persistent "Remember me" cookie
 * signs the user back in instead of showing the login form again.
 */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return router.createUrlTree(['/app']);
  }

  return auth.refreshCurrentUser().pipe(
    map((): boolean | UrlTree => router.createUrlTree(['/app'])),
    catchError((): Observable<boolean | UrlTree> => of(true)),
  );
};
