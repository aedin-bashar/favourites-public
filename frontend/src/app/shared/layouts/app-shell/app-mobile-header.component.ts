import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FavIcons } from '../../icons/fav-icons';
import { AppUserMenuComponent } from './app-user-menu.component';

/**
 * Compact mobile header for authenticated screens.
 * Left: Favourites logo. Right: user avatar/menu.
 * The search bar sits below the header on mobile (not inside this component).
 */
@Component({
  selector: 'app-mobile-header',
  standalone: true,
  imports: [RouterLink, AppUserMenuComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app-mobile-header.component.html',
  styleUrl: './app-mobile-header.component.scss',
})
export class AppMobileHeaderComponent {
  readonly productName = input<string>('Favourites');
  readonly displayName = input<string | null>(null);
  readonly email = input<string | null>(null);
  readonly signOut = output<void>();

  protected readonly icons = FavIcons;

  protected onSignOut(): void {
    this.signOut.emit();
  }
}
