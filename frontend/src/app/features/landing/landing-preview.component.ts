import { ChangeDetectionStrategy, Component } from '@angular/core';

import { FavIcons } from '../../shared/icons/fav-icons';

/**
 * Product-preview composition shown on the right side of the landing hero
 * (design 000). Renders a stylised dashboard mockup using real design tokens
 * — no embedded images.
 */
@Component({
  selector: 'fav-landing-preview',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing-preview.component.html',
  styleUrl: './landing-preview.component.scss',
})
export class LandingPreviewComponent {
  protected readonly icons = FavIcons;
}
