import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Reusable landing-page section wrapper. Provides consistent vertical
 * rhythm, a constrained content width, and optional surface tone for
 * alternating sections. Source: docs/UI_DESIGN_GUIDE.md §6, §7, §19.
 *
 * Inputs:
 *   - id:    anchor id for in-page navigation (Features/How it works/Security)
 *   - tone:  'page' (default page background) | 'surface' (white) | 'alt'
 *   - size:  'default' | 'compact' | 'spacious' — vertical padding scale
 */
@Component({
  selector: 'app-landing-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing-section.component.html',
  styleUrl: './landing-section.component.scss',
})
export class LandingSectionComponent {
  readonly id = input<string | undefined>(undefined);
  readonly tone = input<'page' | 'surface' | 'alt'>('page');
  readonly size = input<'default' | 'compact' | 'spacious'>('default');
}
