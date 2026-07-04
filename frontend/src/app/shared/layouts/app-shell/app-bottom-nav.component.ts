import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MOBILE_NAV_SLOTS, type MobileNavSlot } from './nav-items';

/**
 * Mobile bottom navigation — UI_DESIGN_GUIDE.md §8.
 * Slots: Dashboard, Links, Add (action), Tags, More.
 * 44px+ touch targets per the guide. The Add slot is a centred raised
 * action button that emits `addClick` rather than navigating.
 */
@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app-bottom-nav.component.html',
  styleUrl: './app-bottom-nav.component.scss',
})
export class AppBottomNavComponent {
  readonly addClick = output<void>();
  readonly moreClick = output<void>();

  protected readonly slots = MOBILE_NAV_SLOTS;

  protected onAction(slot: MobileNavSlot): void {
    if (slot.id === 'add') {
      this.addClick.emit();
    } else if (slot.id === 'more') {
      this.moreClick.emit();
    }
  }
}
