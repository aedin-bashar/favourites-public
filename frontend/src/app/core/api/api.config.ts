import { InjectionToken } from '@angular/core';

/**
 * Base URL for the Favourites Web API.
 * Injected by {@link ApiClient} when composing request URLs.
 *
 * The URL follows Angular's `<base href>`, which is set by the deployment
 * pipeline for each host. This keeps the app portable across the home server
 * and VPS public prefixes.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => getBaseHref(),
});

function getBaseHref(): string {
  const href = globalThis.document?.querySelector('base')?.getAttribute('href')?.trim() || '/';
  const origin = globalThis.location?.origin || 'http://localhost';

  let pathname: string;

  try {
    pathname = new URL(href, origin).pathname;
  } catch {
    pathname = href;
  }

  if (!pathname.startsWith('/')) {
    pathname = `/${pathname}`;
  }

  return pathname.replace(/\/+$/, '');
}
