import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LandingHeaderComponent } from './landing-header.component';
import { LandingFooterComponent } from './landing-footer.component';

/**
 * Top-level shell for landing-style pages. Renders the landing header,
 * projects page content via <ng-content>, and renders the landing footer.
 * Source: docs/UI_DESIGN_GUIDE.md §7, §19.
 *
 * Pages stack landing sections inside the shell:
 *
 *   <app-landing-shell>
 *     <app-landing-section id="hero" tone="surface">...</app-landing-section>
 *     <app-landing-section id="features">...</app-landing-section>
 *     ...
 *   </app-landing-shell>
 */
@Component({
  selector: 'app-landing-shell',
  standalone: true,
  imports: [LandingHeaderComponent, LandingFooterComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing-shell.component.html',
  styleUrl: './landing-shell.component.scss',
})
export class LandingShellComponent {
  readonly productName = input<string>('Favourites');
}
