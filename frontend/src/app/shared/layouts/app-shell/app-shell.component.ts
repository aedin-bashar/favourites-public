import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AppSidebarComponent } from './app-sidebar.component';
import { AppTopbarComponent } from './app-topbar.component';
import { AppMobileHeaderComponent } from './app-mobile-header.component';
import { AppBottomNavComponent } from './app-bottom-nav.component';
import { AppSearchComponent } from './app-search.component';

/**
 * Authenticated application shell.
 * Desktop (≥768px): CSS grid — fixed sidebar | flex-1 main | optional right rail slot.
 * Tablet (768–1279px): sidebar + main, no rail.
 * Mobile (<768px): mobile header + bottom nav, no sidebar, no topbar.
 *
 * Right rail: pages project content via <ng-content select="[appRail]" />.
 * The rail column only appears when something is projected into that slot.
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    AppSidebarComponent,
    AppTopbarComponent,
    AppMobileHeaderComponent,
    AppBottomNavComponent,
    AppSearchComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent {
  readonly productName = input<string>('Favourites');
  readonly displayName = input<string | null>(null);
  readonly email = input<string | null>(null);

  readonly signOut = output<void>();
  readonly addClick = output<void>();
  readonly moreClick = output<void>();

  protected onSignOut(): void {
    this.signOut.emit();
  }
  protected onAddClick(): void {
    this.addClick.emit();
  }
  protected onMoreClick(): void {
    this.moreClick.emit();
  }
}
