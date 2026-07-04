import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface FilterOption {
  value: string;
  label: string;
}

export interface FilterDropdown {
  id: string;
  label: string;
  options: FilterOption[];
  selected: string | null;
}

@Component({
  selector: 'fav-filter-toolbar',
  standalone: true,

  template: `
    <div class="filter-toolbar">
      <!-- Search slot -->
      <div class="filter-toolbar__search">
        <ng-content select="[slot=search]"></ng-content>
      </div>

      <!-- Primary filter dropdowns -->
      <div class="filter-toolbar__filters">
        <ng-content select="[slot=filters]"></ng-content>
      </div>

      <!-- Clear filters -->
      @if (hasActiveFilters) {
        <button
          type="button"
          class="filter-toolbar__clear"
          (click)="clearFilters.emit()">
          Clear filters
        </button>
      }
    </div>
  `,
  styleUrl: './filter-toolbar.component.scss',
})
export class FilterToolbarComponent {
  @Input() hasActiveFilters = false;
  @Output() clearFilters = new EventEmitter<void>();
}
