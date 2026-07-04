import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  output,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FavIcons } from '../../icons/fav-icons';

/**
 * Bottom-sheet modal opened from the mobile bottom nav's `More` slot.
 * Shows nav links for Categories, Archived, Settings, and a Sign out action.
 * Traps focus and closes on Escape or backdrop click.
 */
@Component({
  selector: 'fav-more-sheet',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="more-sheet__backdrop" (click)="onClose()" aria-hidden="true"></div>
    <div
      class="more-sheet__panel"
      role="dialog"
      aria-modal="true"
      aria-labelledby="more-sheet-title"
    >
      <div class="more-sheet__header">
        <h2 class="more-sheet__title" id="more-sheet-title">More</h2>
        <button
          #closeBtn
          type="button"
          class="more-sheet__close fav-icon-btn"
          aria-label="Close"
          (click)="onClose()"
        >
          <i [class]="icons.Close" aria-hidden="true"></i>
        </button>
      </div>
      <nav class="more-sheet__nav" aria-label="More navigation">
        <a class="more-sheet__item" routerLink="/app/categories" (click)="onClose()">
          <i [class]="icons.Categories" class="more-sheet__item-icon" aria-hidden="true"></i>
          <span>Categories</span>
        </a>
        <a class="more-sheet__item" routerLink="/app/archived" (click)="onClose()">
          <i [class]="icons.Archived" class="more-sheet__item-icon" aria-hidden="true"></i>
          <span>Archived</span>
        </a>
        <a class="more-sheet__item" routerLink="/app/settings" (click)="onClose()">
          <i [class]="icons.Settings" class="more-sheet__item-icon" aria-hidden="true"></i>
          <span>Settings</span>
        </a>
        <button
          type="button"
          class="more-sheet__item more-sheet__item--danger"
          (click)="onSignOut()"
        >
          <i [class]="icons.SignOut" class="more-sheet__item-icon" aria-hidden="true"></i>
          <span>Sign out</span>
        </button>
      </nav>
    </div>
  `,
  styleUrl: './more-sheet.component.scss',
})
export class MoreSheetComponent implements OnInit {
  readonly close = output<void>();
  readonly signOut = output<void>();

  protected readonly icons = FavIcons;

  @ViewChild('closeBtn') private readonly closeBtnRef?: ElementRef<HTMLButtonElement>;

  ngOnInit(): void {
    setTimeout(() => this.closeBtnRef?.nativeElement.focus(), 0);
  }

  protected onClose(): void {
    this.close.emit();
  }

  protected onSignOut(): void {
    this.signOut.emit();
  }

  @HostListener('document:keydown.escape', ['$event'])
  protected onEscape(event: Event): void {
    if (event.defaultPrevented) return;
    event.preventDefault();
    this.onClose();
  }
}
