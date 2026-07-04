// Frontend mirrors of the backend link contracts.
// Source of truth: src/Favourites.Api/Contracts/Links/LinkContracts.cs
// JSON over the wire is camelCase (ASP.NET Core default).

import type { CategoryResponse } from '../../categories/models/category.models';
import type { TagResponse } from '../../tags/models/tag.models';

/**
 * Body sent to `POST /api/links` to create a new favourite link.
 *
 * `UserId` is intentionally absent — the server reads the authenticated
 * user from the auth cookie and ignores any client-supplied owner.
 */
export interface CreateLinkRequest {
  readonly url: string;
  readonly title: string;
  readonly description?: string | null;
  readonly tagIds?: readonly string[] | null;
  readonly categoryId?: string | null;
}

/**
 * Body sent to `PUT /api/links/{id}` to update an existing favourite link.
 *
 * Mirrors the backend `UpdateLinkRequest` contract record. The id sits in
 * the route — only the editable fields are in the body. Sending
 * `description: null` clears a previously-set description; the same applies
 * to `categoryId: null` for the category assignment.
 */
export interface UpdateLinkRequest {
  readonly url: string;
  readonly title: string;
  readonly description: string | null;
  readonly tagIds?: readonly string[] | null;
  readonly categoryId?: string | null;
}

/**
 * Shape of a favourite link as returned by the API. Mirrors the backend
 * `LinkResponse` contract record field-for-field.
 *
 * `createdAtUtc` and `updatedAtUtc` arrive as ISO-8601 strings (System.Text.Json
 * serializes `DateTimeOffset` to RFC 3339). They are kept as strings here so
 * the model stays a faithful wire representation; pages convert to `Date`
 * only at the rendering boundary.
 */
export interface LinkResponse {
  readonly id: string;
  readonly url: string;
  readonly title: string;
  readonly description: string | null;
  readonly isArchived: boolean;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string | null;
  readonly tags: readonly TagResponse[];
  readonly category: CategoryResponse | null;
}
