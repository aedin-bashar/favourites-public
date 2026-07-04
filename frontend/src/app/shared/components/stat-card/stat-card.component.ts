import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

export type StatCardTone = 'teal' | 'indigo' | 'amber' | 'rose';

@Component({
  selector: 'fav-stat-card',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    @if (link) {
      <a class="stat-card stat-card--clickable" [attr.data-tone]="tone" [routerLink]="link" [attr.aria-label]="label">
        <div class="stat-card__icon-wrap">
          <i class="fa-solid {{ icon }}"></i>
        </div>
        <div class="stat-card__body">
          <div class="stat-card__value">{{ value }}</div>
          <div class="stat-card__label">{{ label }}</div>
          <ng-content></ng-content>
        </div>
      </a>
    } @else {
      <div class="stat-card" [attr.data-tone]="tone">
        <div class="stat-card__icon-wrap">
          <i class="fa-solid {{ icon }}"></i>
        </div>
        <div class="stat-card__body">
          <div class="stat-card__value">{{ value }}</div>
          <div class="stat-card__label">{{ label }}</div>
          <ng-content></ng-content>
        </div>
      </div>
    }
  `,
  styleUrl: './stat-card.component.scss',
})
export class StatCardComponent {
  @Input({ required: true }) label!: string;
  @Input({ required: true }) value!: string | number;
  @Input({ required: true }) icon!: string;
  @Input() tone: StatCardTone = 'teal';
  @Input() link: string | string[] | null = null;
}
