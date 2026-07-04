import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FavIcons } from '../../icons/fav-icons';

/**
 * Landing-page header — logo/name on the left, primary nav (Features,
 * How it works, Security) center-right, and auth actions (Sign in,
 * Register) on the far right. Source: docs/UI_DESIGN_GUIDE.md §9, §19.
 *
 * Mobile collapses to logo + menu icon per UI guide §8. The menu button is
 * exposed via a click event so the parent shell decides what opens.
 */
@Component({
  selector: 'app-landing-header',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing-header.component.html',
  styleUrl: './landing-header.component.scss',
})
export class LandingHeaderComponent {
  readonly productName = input<string>('Favourites');
  protected readonly icons = FavIcons;
}
