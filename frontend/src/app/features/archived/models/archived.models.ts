// Frontend mirrors of the backend archived contracts.
// Source of truth: src/Favourites.Api/Contracts/Links/
// JSON over the wire is camelCase (ASP.NET Core default).

import type { LinkResponse } from '../../links/models/link.models';

/**
 * Summary data returned by `GET /api/archived/summary`.
 * Mirrors the ArchivedSummaryResponse backend contract.
 */
export interface ArchivedSummaryResponse {
  readonly archivedLinks: number;
  readonly archivedThisMonth: number;
  readonly oldestArchived: LinkResponse | null;
  readonly restoredRecently: number;
  readonly cleanupSuggestions: readonly LinkResponse[];
}
