import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { DatePipe } from '@angular/common';

import { FavIcons } from '../../../shared/icons/fav-icons';
import { FaviconUrlPipe } from '../../../shared/pipes/favicon-url.pipe';
import { UserPreferencesService } from '../../settings/services/user-preferences.service';
import type { LinkResponse } from '../models/link.models';

/**
 * Single saved-link card.
 *
 * Renders one {@link LinkResponse} with title, domain, description, created
 * date, and the four action buttons from UI Design Guide §13: open, edit,
 * archive, delete. The card itself owns no state — every action emits an
 * output so the parent ({@link LinkListComponent}) decides what
 * to do. Open emits the link as a convenience, but the template also marks
 * the title as a real anchor so users can middle-click / open in a new tab
 * with the browser's normal affordances.
 */
@Component({
  selector: 'app-link-card',
  standalone: true,
  imports: [DatePipe, FaviconUrlPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './link-card.component.html',
  styleUrl: './link-card.component.scss',
})
export class LinkCardComponent {
  readonly link = input.required<LinkResponse>();
  // Disables the archive/restore button while the parent is processing
  // a state change for this specific link. Stays `false` for
  // cards that are not the in-flight target so the rest of the list is
  // still interactive.
  readonly archiveBusy = input<boolean>(false);

  readonly opened = output<LinkResponse>();
  readonly editRequested = output<LinkResponse>();
  readonly archiveRequested = output<LinkResponse>();
  readonly restoreRequested = output<LinkResponse>();
  readonly deleteRequested = output<LinkResponse>();

  protected readonly icons = FavIcons;
  // Display preferences from Settings: favicon visibility and open target.
  protected readonly preferences = inject(UserPreferencesService).preferences;

  protected readonly domain = computed(() => extractDomain(this.link().url));

  protected onOpen(): void {
    this.opened.emit(this.link());
  }

  protected onEdit(): void {
    this.editRequested.emit(this.link());
  }

  protected onArchive(): void {
    this.archiveRequested.emit(this.link());
  }

  protected onRestore(): void {
    this.restoreRequested.emit(this.link());
  }

  protected onDelete(): void {
    this.deleteRequested.emit(this.link());
  }

  protected onFaviconError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.display = 'none';
    const fallback = img.nextElementSibling as HTMLElement | null;
    if (fallback) fallback.style.display = '';
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
