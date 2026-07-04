import { Component, EventEmitter, Input, Output } from '@angular/core';

export type TileAccentTone =
  | 'purple'
  | 'indigo'
  | 'teal'
  | 'sky'
  | 'amber'
  | 'rose'
  | 'lime'
  | 'green';

export interface TileCardMenuItem {
  label: string;
  danger?: boolean;
  disabled?: boolean;
}

@Component({
  selector: 'fav-tile-card',
  standalone: true,
  template: `
    <div class="tile-card" [attr.data-tone]="tone">
      <!-- 3-dot menu -->
      @if (menuItems.length) {
        <div class="tile-card__menu">
          <div class="dropdown">
            <button
              class="tile-card__menu-btn btn btn-link p-0"
              type="button"
              data-bs-toggle="dropdown"
              aria-expanded="false"
              aria-label="More options"
            >
              <i class="fa-solid fa-ellipsis-vertical"></i>
            </button>
            <ul class="dropdown-menu dropdown-menu-end">
              @for (item of menuItems; track item.label) {
                <li>
                  <button
                    class="dropdown-item"
                    [class.text-danger]="item.danger"
                    type="button"
                    [disabled]="item.disabled"
                    [attr.aria-disabled]="item.disabled ? 'true' : null"
                    (click)="menuAction.emit(item)"
                  >
                    {{ item.label }}
                  </button>
                </li>
              }
            </ul>
          </div>
        </div>
      }

      <!-- Icon circle -->
      <div class="tile-card__icon-wrap">
        <i class="fa-solid {{ icon }}"></i>
      </div>

      <!-- Content -->
      <div class="tile-card__body">
        <div class="tile-card__name">{{ name }}</div>
        <div class="tile-card__subtitle">{{ subtitle }}</div>
        @if (meta) {
          <div class="tile-card__meta">{{ meta }}</div>
        }
      </div>

      <!-- Primary action -->
      @if (actionLabel) {
        <div class="tile-card__action">
          <button class="btn btn-link tile-card__action-link p-0" type="button" (click)="action.emit()">
            {{ actionLabel }} ›
          </button>
        </div>
      }
    </div>
  `,
  styleUrl: './tile-card.component.scss',
})
export class TileCardComponent {
  @Input({ required: true }) icon!: string;
  @Input({ required: true }) name!: string;
  @Input({ required: true }) subtitle!: string;
  @Input() tone: TileAccentTone = 'teal';
  @Input() meta: string = '';
  @Input() actionLabel: string = '';
  @Input() menuItems: TileCardMenuItem[] = [];

  @Output() action = new EventEmitter<void>();
  @Output() menuAction = new EventEmitter<TileCardMenuItem>();
}
