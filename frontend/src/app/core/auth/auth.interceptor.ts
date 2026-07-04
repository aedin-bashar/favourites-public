import { inject } from '@angular/core';
import type { HttpInterceptorFn } from '@angular/common/http';
import { API_BASE_URL } from '../api/api.config';

/**
 * Cookie-based auth interceptor (ADR-002).
 *
 * The backend issues an httpOnly `.AspNetCore.Identity.Application` cookie
 * on a successful login. For the browser to include it on subsequent calls,
 * `withCredentials` MUST be true on the XHR/fetch request. Cookies are also
 * a CORS-credentialed resource, so this only kicks in for requests aimed at
 * the configured API base URL (or same-origin `/api/...` paths in
 * production behind Nginx).
 *
 * This interceptor is registered globally in `app.config.ts`.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const apiBaseUrl = inject(API_BASE_URL);
  if (shouldAttachCredentials(req.url, apiBaseUrl)) {
    return next(req.clone({ withCredentials: true }));
  }
  return next(req);
};

function shouldAttachCredentials(url: string, apiBaseUrl: string): boolean {
  // Same-origin '/api/...' requests (e.g. production behind Nginx).
  if (url.startsWith('/api/')) {
    return true;
  }
  // Absolute requests aimed at the configured API base URL.
  if (apiBaseUrl && url.startsWith(apiBaseUrl)) {
    return true;
  }
  return false;
}
