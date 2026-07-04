/**
 * Derives a human-readable title from a URL when the user hasn't typed one.
 *
 * Used by the add-link form and dashboard quick-add so
 * pasting a URL is enough to save a link — the user can edit the suggested
 * title afterwards if they want a better one.
 *
 * Priority:
 *   1. Last non-empty path segment, prettified (dashes/underscores → spaces).
 *   2. Hostname without `www.` prefix.
 *   3. The original input, trimmed (returned only when URL parsing fails).
 *
 * Returns an empty string for empty input so callers can detect "nothing to
 * suggest" easily.
 */
export function deriveTitleFromUrl(raw: string): string {
  const trimmed = raw.trim();
  if (!trimmed) return '';

  let parsed: URL;
  try {
    parsed = new URL(trimmed);
  } catch {
    return trimmed;
  }

  if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
    return trimmed;
  }

  const segments = parsed.pathname.split('/').filter((segment) => segment.length > 0);
  const lastSegment = segments.length > 0 ? segments[segments.length - 1] : '';
  if (lastSegment) {
    const withoutExt = lastSegment.replace(/\.[a-z0-9]{1,5}$/i, '');
    const pretty = decodeURIComponentSafe(withoutExt).replace(/[-_]+/g, ' ').trim();
    if (pretty.length > 0) {
      return capitalize(pretty);
    }
  }

  const host = parsed.hostname.replace(/^www\./i, '');
  return host || trimmed;
}

function decodeURIComponentSafe(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
