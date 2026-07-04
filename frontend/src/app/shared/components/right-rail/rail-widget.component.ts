import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'fav-rail-widget',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="rail-widget">
      <div class="rail-widget__header">
        <span class="rail-widget__title">{{ title }}</span>
        <ng-content select="[slot=action]"></ng-content>
      </div>
      <div class="rail-widget__body">
        <ng-content></ng-content>
      </div>
    </div>
  `,
  styleUrl: './rail-widget.component.scss',
})
export class RailWidgetComponent {
  @Input({ required: true }) title!: string;
}
