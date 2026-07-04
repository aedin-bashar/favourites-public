import { Component, Input } from '@angular/core';

@Component({
  selector: 'fav-status-pill',
  standalone: true,
  template: `
    <span class="status-pill" [class]="'status-pill--' + normalizedStatus">
      {{ label }}
    </span>
  `,
  styleUrl: './status-pill.component.scss',
})
export class StatusPillComponent {
  @Input({ required: true }) status!: 'active' | 'archived' | string;

  get normalizedStatus(): string {
    return this.status?.toLowerCase() ?? 'active';
  }

  get label(): string {
    const s = this.normalizedStatus;
    return s.charAt(0).toUpperCase() + s.slice(1);
  }
}
