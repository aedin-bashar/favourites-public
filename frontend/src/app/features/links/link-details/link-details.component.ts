import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { FavIcons } from '../../../shared/icons/fav-icons';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import { EditLinkFormComponent } from '../edit-link-form/edit-link-form.component';
import { FocusTrapDirective } from '../../../shared/directives/focus-trap.directive';
import type { LinkResponse } from '../models/link.models';
import { LinksApiService } from '../services/links-api.service';

/**
 * Link details page.
 *
 * Reads `:id` from the route, calls {@link LinksApiService.getById}, and
 * renders the full link record (title, URL, full untruncated description,
 * archive state, created / updated timestamps).
 *
 * The four UI surfaces mirror the list page so the user gets the same
 * loading / error / not-found / content patterns: a spinner block while the
 * GET is in flight, a retry alert on transport / 5xx, a friendly "this link
 * is no longer available" empty state on 404 (covers both missing ids and
 * links owned by another user — the backend collapses both into 404 by the
 * ownership-not-found rule, see [docs/ARCHITECTURE.md]), and the details
 * card itself on success.
 *
 * The details card hosts the **edit** flow: clicking Edit
 * swaps the read-only details for the {@link EditLinkFormComponent}; on
 * success the new link replaces the displayed copy. The **delete** flow
 * opens a confirmation modal — confirming calls the
 * API, then navigates back to the list. The **archive** button toggles:
 * it archives an active link and restores an archived one, mirroring the
 * row actions on the list and archived pages.
 */
@Component({
  selector: 'app-link-details',
  standalone: true,
  imports: [DatePipe, RouterLink, EditLinkFormComponent, FocusTrapDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './link-details.component.html',
  styleUrl: './link-details.component.scss',
})
export class LinkDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly linksApi = inject(LinksApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly icons = FavIcons;
  // Display/behaviour preferences from Settings (open target, delete confirm).
  protected readonly preferences = inject(UserPreferencesService).preferences;

  protected readonly link = signal<LinkResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly notFound = signal(false);

  protected readonly editing = signal(false);
  protected readonly deleteConfirmOpen = signal(false);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  protected readonly archiving = signal(false);
  protected readonly archiveError = signal<string | null>(null);

  protected readonly domain = computed(() => {
    const current = this.link();
    return current ? extractDomain(current.url) : '';
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loading.set(false);
      this.notFound.set(true);
      return;
    }
    this.load(id);
  }

  protected reload(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.load(id);
    }
  }

  protected startEditing(): void {
    if (!this.link()) return;
    this.editing.set(true);
  }

  protected onLinkUpdated(updated: LinkResponse): void {
    this.link.set(updated);
    this.editing.set(false);
  }

  protected onEditCancelled(): void {
    this.editing.set(false);
  }

  protected toggleArchive(): void {
    const current = this.link();
    if (!current || this.archiving()) return;

    const action = current.isArchived ? 'restore' : 'archive';
    this.archiving.set(true);
    this.archiveError.set(null);

    const request = current.isArchived
      ? this.linksApi.restore(current.id)
      : this.linksApi.archive(current.id);

    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.archiving.set(false);
        this.link.set({ ...current, isArchived: !current.isArchived });
      },
      error: (err: unknown) => {
        this.archiving.set(false);
        this.archiveError.set(formatArchiveError(err, action));
      },
    });
  }

  protected openDeleteConfirm(): void {
    if (!this.link()) return;
    this.deleteError.set(null);
    // "Confirm before delete" preference (Settings → Link defaults): when
    // switched off, delete straight away without the confirmation modal.
    if (!this.preferences().confirmBeforeDelete) {
      this.confirmDelete();
      return;
    }
    this.deleteConfirmOpen.set(true);
  }

  protected closeDeleteConfirm(): void {
    if (this.deleting()) return;
    this.deleteConfirmOpen.set(false);
  }

  protected confirmDelete(): void {
    const current = this.link();
    if (!current || this.deleting()) return;

    this.deleting.set(true);
    this.deleteError.set(null);
    this.linksApi
      .remove(current.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deleting.set(false);
          this.deleteConfirmOpen.set(false);
          this.router.navigate(['/app/links']);
        },
        error: (err: unknown) => {
          this.deleting.set(false);
          this.deleteError.set(formatDeleteError(err));
        },
      });
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.notFound.set(false);
    this.editing.set(false);
    this.linksApi
      .getById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.link.set(response);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          const status = readStatus(err);
          if (status === 404) {
            this.notFound.set(true);
          } else {
            this.error.set(formatDetailsError(status));
          }
          this.loading.set(false);
        },
      });
  }
}

function extractDomain(url: string): string {
  try {
    const host = new URL(url).hostname;
    return host.startsWith('www.') ? host.slice(4) : host;
  } catch {
    return url;
  }
}

function readStatus(err: unknown): number | null {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    return typeof status === 'number' ? status : null;
  }
  return null;
}

function formatDetailsError(status: number | null): string {
  if (status === 401) {
    return 'Your session has expired. Sign in again to view this link.';
  }
  if (status === 0) {
    return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while loading this link. Please try again.';
}

function formatArchiveError(err: unknown, action: 'archive' | 'restore'): string {
  const status = readStatus(err);
  if (status === 401) {
    return `Your session has expired. Sign in again to ${action} this link.`;
  }
  if (status === 404) {
    return 'This link is no longer available. It may have been removed.';
  }
  if (status === 0) {
    return 'Could not reach the server. Check your connection and try again.';
  }
  return `Something went wrong while trying to ${action} the link. Please try again.`;
}

function formatDeleteError(err: unknown): string {
  const status = readStatus(err);
  if (status === 401) {
    return 'Your session has expired. Sign in again to delete this link.';
  }
  if (status === 404) {
    return 'This link is no longer available. It may have already been deleted.';
  }
  if (status === 0) {
    return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while deleting the link. Please try again.';
}
