import { computed, Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiClient } from '../api/api-client.service';
import type {
  AuthUser,
  CurrentUserResponse,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  LogoutResponse,
  RegisterRequest,
  RegisterResponse,
  ResetPasswordRequest,
} from './auth.types';

/**
 * Frontend authentication service. Wraps the backend `/api/auth/*` endpoints
 * and exposes the authenticated user as a signal so guards, layout shells,
 * and pages can react to sign-in / sign-out without a manual subscription.
 *
 * Auth approach: secure cookie-based authentication (ADR-002). The cookie is
 * set by the server on a successful login and cleared on logout. The
 * `AuthInterceptor` ensures every API request is sent with `withCredentials`.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);

  private readonly _user = signal<AuthUser | null>(null);

  /** Currently authenticated user, or `null` if not signed in / not yet probed. */
  readonly user = this._user.asReadonly();

  /** True iff a user is currently held in service state. */
  readonly isAuthenticated = computed(() => this._user() !== null);

  register(payload: RegisterRequest): Observable<RegisterResponse> {
    return this.api
      .post<RegisterResponse, RegisterRequest>('/api/auth/register', payload)
      .pipe(tap((res) => this._user.set(toUser(res))));
  }

  login(payload: LoginRequest): Observable<LoginResponse> {
    return this.api
      .post<LoginResponse, LoginRequest>('/api/auth/login', payload)
      .pipe(tap((res) => this._user.set(toUser(res))));
  }

  logout(): Observable<LogoutResponse> {
    return this.api
      .post<LogoutResponse, Record<string, never>>('/api/auth/logout', {})
      .pipe(tap(() => this._user.set(null)));
  }

  /**
   * Fetches the authenticated user from the backend and updates local state.
   * Used on app start (rehydrate the user from an existing cookie) and by
   * the auth guard when state is empty but a cookie might still be valid.
   */
  refreshCurrentUser(): Observable<CurrentUserResponse> {
    return this.api
      .get<CurrentUserResponse>('/api/auth/current-user')
      .pipe(tap((res) => this._user.set(toUser(res))));
  }

  forgotPassword(payload: ForgotPasswordRequest): Observable<void> {
    return this.api.post<void, ForgotPasswordRequest>('/api/auth/forgot-password', payload);
  }

  resetPassword(payload: ResetPasswordRequest): Observable<void> {
    return this.api.post<void, ResetPasswordRequest>('/api/auth/reset-password', payload);
  }

  /** Clears the locally-held user without calling the backend. */
  clearLocalUser(): void {
    this._user.set(null);
  }
}

function toUser(res: { id: string; displayName: string; email: string }): AuthUser {
  return { id: res.id, displayName: res.displayName, email: res.email };
}
