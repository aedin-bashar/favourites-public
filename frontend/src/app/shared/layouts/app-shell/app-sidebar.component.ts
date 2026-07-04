import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { FavIcons } from '../../icons/fav-icons';
import { APP_NAV_ITEMS } from './nav-items';

/**
 * Desktop left sidebar.
 * Logo block at top, nav list with active-state styling per UI guide §9,
 * user identity card at bottom (avatar initials, display name, email, settings icon).
 * No "Add to Chrome" promo — the browser extension does not exist yet.
 */
@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app-sidebar.component.html',
  styleUrl: './app-sidebar.component.scss',
})
export class AppSidebarComponent {
  readonly productName = input<string>('Favourites');
  readonly displayName = input<string | null>(null);
  readonly email = input<string | null>(null);

  protected readonly icons = FavIcons;
  protected readonly navItems = APP_NAV_ITEMS;

  protected initials(): string {
    const name = this.displayName();
    if (!name) return '?';
    return name
      .split(' ')
      .slice(0, 2)
      .map((w) => w[0]?.toUpperCase() ?? '')
      .join('');
  }
}
