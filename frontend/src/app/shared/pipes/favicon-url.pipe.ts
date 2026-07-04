import { Pipe, PipeTransform } from '@angular/core';

/** Transparent 1x1 GIF used when a link URL cannot be parsed — never send a raw value upstream. */
const BLANK_IMAGE = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';

/**
 * Builds the favicon URL for a saved link, disclosing only the hostname to the
 * favicon service. The full link URL (path/query) must never leave the app.
 */
@Pipe({ name: 'faviconUrl', standalone: true, pure: true })
export class FaviconUrlPipe implements PipeTransform {
  transform(url: string, size = 32): string {
    let host: string;
    try {
      host = new URL(url).hostname;
    } catch {
      return BLANK_IMAGE;
    }
    return `https://www.google.com/s2/favicons?domain=${encodeURIComponent(host)}&sz=${size}`;
  }
}
