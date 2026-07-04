import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaginationComponent } from './pagination.component';

function create(page: number, pageSize: number, total: number): ComponentFixture<PaginationComponent> {
  const f = TestBed.createComponent(PaginationComponent);
  f.componentRef.setInput('page', page);
  f.componentRef.setInput('pageSize', pageSize);
  f.componentRef.setInput('total', total);
  f.detectChanges();
  return f;
}

describe('PaginationComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PaginationComponent] }).compileComponents();
  });

  it('renders nothing when total is 0', () => {
    const f = create(1, 25, 0);
    expect(f.nativeElement.querySelector('.pagination-strip')).toBeNull();
  });

  it('shows correct "Showing X–Y of Z" text', () => {
    const f = create(2, 10, 35);
    const count: HTMLElement = f.nativeElement.querySelector('.pagination-strip__count');
    expect(count.textContent?.trim()).toBe('Showing 11–20 of 35');
  });

  it('calculates total pages correctly', () => {
    const f = create(1, 10, 35);
    expect(f.componentInstance.totalPages).toBe(4);
  });

  it('emits pageChange on page button click', () => {
    const f = create(1, 10, 50);
    const emitted: number[] = [];
    f.componentInstance.pageChange.subscribe((p: number) => emitted.push(p));

    const pageBtns: NodeListOf<HTMLButtonElement> =
      f.nativeElement.querySelectorAll('button.page-btn:not(.page-btn--nav)');
    // Page buttons: 1 (active), 2, 3, 4, 5 — click page 2
    pageBtns[1].click();
    expect(emitted).toEqual([2]);
  });

  it('disables prev button on first page', () => {
    const f = create(1, 10, 30);
    const navBtns: NodeListOf<HTMLButtonElement> =
      f.nativeElement.querySelectorAll('button.page-btn--nav');
    expect(navBtns[0].disabled).toBe(true);
  });

  it('disables next button on last page', () => {
    const f = create(3, 10, 30);
    const navBtns: NodeListOf<HTMLButtonElement> =
      f.nativeElement.querySelectorAll('button.page-btn--nav');
    expect(navBtns[1].disabled).toBe(true);
  });

  it('shows ellipsis for large page counts', () => {
    const f = create(5, 10, 200);
    const ellipses = f.nativeElement.querySelectorAll('.page-ellipsis');
    expect(ellipses.length).toBeGreaterThanOrEqual(1);
  });
});
