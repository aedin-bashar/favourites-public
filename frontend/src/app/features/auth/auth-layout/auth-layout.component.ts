import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FavIcons } from '../../../shared/icons/fav-icons';

/**
 * Reusable centred-card layout for unauthenticated pages (login, register).
 * Source: docs/UI_DESIGN_GUIDE.md §6, §19 (Login/Register pages).
 *
 * Projection slots:
 *   - `[authLayoutHeading]` (h1 title text)
 *   - `[authLayoutSubheading]` (one-line supporting copy)
 *   - default slot — the form
 *   - `[authLayoutFooter]` — secondary link (e.g. "New here? Sign up")
 */
@Component({
  selector: 'app-auth-layout',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './auth-layout.component.html',
  styleUrl: './auth-layout.component.scss',
})
export class AuthLayoutComponent {
  readonly productName = input<string>('Favourites');
  protected readonly icons = FavIcons;
}
