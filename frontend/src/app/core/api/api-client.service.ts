import { HttpClient, type HttpContext, type HttpHeaders, type HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_BASE_URL } from './api.config';

/**
 * Options forwarded to the underlying `HttpClient` call.
 * `withCredentials` defaults to `true` so the auth cookie is always sent —
 * the {@link AuthInterceptor} also enforces this for safety.
 */
export interface ApiRequestOptions {
  readonly headers?: HttpHeaders | Record<string, string | string[]>;
  readonly params?: HttpParams | Record<string, string | number | boolean | ReadonlyArray<string | number | boolean>>;
  readonly context?: HttpContext;
  readonly observe?: 'body';
  readonly responseType?: 'json';
  readonly reportProgress?: boolean;
  readonly withCredentials?: boolean;
}

/**
 * Thin convenience wrapper around `HttpClient` that prepends the configured
 * API base URL to every request and defaults to cookie-credentialed calls.
 * Feature services inject this rather than `HttpClient` directly so that:
 *
 *   - URL composition lives in one place
 *   - It's a single seam for adding things like global error handling,
 *     correlation IDs, retry policies, etc. later.
 *
 * Usage:
 *   constructor(private readonly api = inject(ApiClient)) {}
 *   this.api.post<LoginResponse, LoginRequest>('/api/auth/login', body)
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  get<TResponse>(path: string, options?: ApiRequestOptions): Observable<TResponse> {
    return this.http.get<TResponse>(this.url(path), this.merge(options));
  }

  post<TResponse, TBody = unknown>(path: string, body: TBody, options?: ApiRequestOptions): Observable<TResponse> {
    return this.http.post<TResponse>(this.url(path), body, this.merge(options));
  }

  put<TResponse, TBody = unknown>(path: string, body: TBody, options?: ApiRequestOptions): Observable<TResponse> {
    return this.http.put<TResponse>(this.url(path), body, this.merge(options));
  }

  patch<TResponse, TBody = unknown>(path: string, body: TBody, options?: ApiRequestOptions): Observable<TResponse> {
    return this.http.patch<TResponse>(this.url(path), body, this.merge(options));
  }

  delete<TResponse>(path: string, options?: ApiRequestOptions): Observable<TResponse> {
    return this.http.delete<TResponse>(this.url(path), this.merge(options));
  }

  /** Upload FormData (multipart). Do NOT set Content-Type — the browser sets it with the boundary. */
  postForm<TResponse>(path: string, body: FormData): Observable<TResponse> {
    return this.http.post<TResponse>(this.url(path), body, { withCredentials: true });
  }

  /** Download a binary response as a Blob (for file exports). */
  getBlob(path: string): Observable<Blob> {
    return this.http.get(this.url(path), { responseType: 'blob', withCredentials: true });
  }

  private url(path: string): string {
    if (/^https?:\/\//i.test(path)) {
      return path;
    }
    const prefix = path.startsWith('/') ? '' : '/';
    return `${this.baseUrl}${prefix}${path}`;
  }

  private merge(options?: ApiRequestOptions): ApiRequestOptions {
    return { withCredentials: true, ...options };
  }
}
