import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AppSearchComponent } from './app-search.component';
import { AppUserMenuComponent } from './app-user-menu.component';

/**
 * Global top bar for all authenticated pages.
 * Layout: [Logo] | [Search — centre] | [UserMenu]
 */
@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [AppSearchComponent, AppUserMenuComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app-topbar.component.html',
  styleUrl: './app-topbar.component.scss',
})
export class AppTopbarComponent {
  readonly displayName = input<string>('');
  readonly email = input<string>('');
  readonly signOut = output<void>();

  protected onSignOut(): void {
    this.signOut.emit();
  }
}
