import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FavIcons } from '../../icons/fav-icons';

/**
 * User avatar + dropdown menu in the top bar.
 * Shows display name, email, a Settings link, and a Sign out action.
 */
@Component({
  selector: 'fav-user-menu',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="user-menu">
      <button
        type="button"
        class="user-menu__trigger"
        [class.user-menu__trigger--open]="open()"
        [attr.aria-expanded]="open()"
        aria-label="User menu"
        (click)="toggle()"
      >
        <span class="user-menu__avatar" aria-hidden="true">
          {{ initials() }}
        </span>
        <span class="user-menu__name">{{ displayName() }}</span>
        <i [class]="icons.CaretDown" class="user-menu__caret" aria-hidden="true"></i>
      </button>

      @if (open()) {
        <div class="user-menu__backdrop" (click)="close()" aria-hidden="true"></div>
        <div class="user-menu__dropdown" role="menu">
          <div class="user-menu__info">
            <span class="user-menu__info-name">{{ displayName() }}</span>
            <span class="user-menu__info-email">{{ email() }}</span>
          </div>
          <div class="user-menu__divider" role="separator"></div>
          <a
            class="user-menu__item"
            role="menuitem"
            routerLink="/app/settings"
            (click)="close()"
          >
            <i [class]="icons.Settings" class="user-menu__item-icon" aria-hidden="true"></i>
            Settings
          </a>
          <div class="user-menu__divider" role="separator"></div>
          <button
            type="button"
            class="user-menu__item user-menu__item--danger"
            role="menuitem"
            (click)="onSignOut()"
          >
            <i [class]="icons.SignOut" class="user-menu__item-icon" aria-hidden="true"></i>
            Sign out
          </button>
        </div>
      }
    </div>
  `,
  styleUrl: './app-user-menu.component.scss',
})
export class AppUserMenuComponent {
  readonly displayName = input<string>('');
  readonly email = input<string>('');
  readonly signOut = output<void>();

  protected readonly icons = FavIcons;
  protected readonly open = signal(false);

  protected initials(): string {
    const name = this.displayName();
    if (!name) return '?';
    return name
      .split(' ')
      .slice(0, 2)
      .map((w) => w[0]?.toUpperCase() ?? '')
      .join('');
  }

  protected toggle(): void {
    this.open.update((v) => !v);
  }

  protected close(): void {
    this.open.set(false);
  }

  protected onSignOut(): void {
    this.close();
    this.signOut.emit();
  }
}
