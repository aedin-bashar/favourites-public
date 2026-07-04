import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'fav-pagination',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="pagination-strip" *ngIf="totalPages > 0">
      <span class="pagination-strip__count">
        Showing {{ showingFrom }}–{{ showingTo }} of {{ total }}
      </span>

      <nav class="pagination-strip__nav" aria-label="Page navigation">
        <button
          type="button"
          class="page-btn page-btn--nav"
          [disabled]="page <= 1"
          (click)="go(page - 1)"
          aria-label="Previous page">
          <i class="fa-solid fa-chevron-left"></i>
        </button>

        <ng-container *ngFor="let p of pages">
          <span *ngIf="p === null" class="page-ellipsis">…</span>
          <button
            *ngIf="p !== null"
            type="button"
            class="page-btn"
            [class.page-btn--active]="p === page"
            (click)="go(p)"
            [attr.aria-current]="p === page ? 'page' : null">
            {{ p }}
          </button>
        </ng-container>

        <button
          type="button"
          class="page-btn page-btn--nav"
          [disabled]="page >= totalPages"
          (click)="go(page + 1)"
          aria-label="Next page">
          <i class="fa-solid fa-chevron-right"></i>
        </button>
      </nav>
    </div>
  `,
  styleUrl: './pagination.component.scss',
})
export class PaginationComponent implements OnChanges {
  @Input({ required: true }) page!: number;
  @Input({ required: true }) pageSize!: number;
  @Input({ required: true }) total!: number;
  @Output() pageChange = new EventEmitter<number>();

  totalPages = 0;
  pages: (number | null)[] = [];
  showingFrom = 0;
  showingTo = 0;

  ngOnChanges(): void {
    this.totalPages = this.total > 0 ? Math.ceil(this.total / this.pageSize) : 0;
    this.showingFrom = this.total === 0 ? 0 : (this.page - 1) * this.pageSize + 1;
    this.showingTo = Math.min(this.page * this.pageSize, this.total);
    this.pages = this.buildPages();
  }

  go(p: number): void {
    if (p < 1 || p > this.totalPages || p === this.page) return;
    this.pageChange.emit(p);
  }

  private buildPages(): (number | null)[] {
    const n = this.totalPages;
    const cur = this.page;
    if (n <= 7) return Array.from({ length: n }, (_, i) => i + 1);

    const pages: (number | null)[] = [1];
    if (cur > 3) pages.push(null);
    const start = Math.max(2, cur - 1);
    const end = Math.min(n - 1, cur + 1);
    for (let i = start; i <= end; i++) pages.push(i);
    if (cur < n - 2) pages.push(null);
    pages.push(n);
    return pages;
  }
}
