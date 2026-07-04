import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ViewMode, ViewModeService, ViewPageId } from './view-mode.service';

@Component({
  selector: 'fav-view-toggle',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="view-toggle" role="group" aria-label="View mode">
      <button
        type="button"
        class="view-toggle__btn"
        [class.view-toggle__btn--active]="current === 'list'"
        (click)="select('list')"
        aria-label="List view"
        [attr.aria-pressed]="current === 'list'">
        <i class="fa-solid fa-list"></i>
      </button>
      <button
        type="button"
        class="view-toggle__btn"
        [class.view-toggle__btn--active]="current === 'cards'"
        (click)="select('cards')"
        aria-label="Cards view"
        [attr.aria-pressed]="current === 'cards'">
        <i class="fa-solid fa-grip"></i>
      </button>
    </div>
  `,
  styleUrl: './view-toggle.component.scss',
})
export class ViewToggleComponent implements OnInit {
  @Input({ required: true }) pageId!: ViewPageId;
  @Output() modeChange = new EventEmitter<ViewMode>();

  current: ViewMode = 'list';

  constructor(private viewMode: ViewModeService) {}

  ngOnInit(): void {
    this.current = this.viewMode.get(this.pageId);
  }

  select(mode: ViewMode): void {
    if (this.current === mode) return;
    this.current = mode;
    this.viewMode.set(this.pageId, mode);
    this.modeChange.emit(mode);
  }
}
