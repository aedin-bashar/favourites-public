import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Landing-page footer — organized columns of product/company/legal links
 * and a copyright row at the bottom. Source: docs/UI_DESIGN_GUIDE.md §7, §19.
 *
 * Concrete column content is intentionally minimal at this stage; the
 * structure (rows/columns, spacing, divider, copyright) is the reusable
 * piece.
 */
@Component({
  selector: 'app-landing-footer',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing-footer.component.html',
  styleUrl: './landing-footer.component.scss',
})
export class LandingFooterComponent {
  readonly productName = input<string>('Favourites');
  protected readonly currentYear = new Date().getFullYear();
}
