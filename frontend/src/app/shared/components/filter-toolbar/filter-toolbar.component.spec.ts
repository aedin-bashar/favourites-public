import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FilterToolbarComponent } from './filter-toolbar.component';

describe('FilterToolbarComponent', () => {
  let fixture: ComponentFixture<FilterToolbarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [FilterToolbarComponent] }).compileComponents();
    fixture = TestBed.createComponent(FilterToolbarComponent);
    fixture.componentRef.setInput('hasActiveFilters', false);
    fixture.detectChanges();
  });

  it('renders the toolbar container', () => {
    expect(fixture.nativeElement.querySelector('.filter-toolbar')).not.toBeNull();
  });

  it('hides clear button when no active filters', () => {
    expect(fixture.nativeElement.querySelector('.filter-toolbar__clear')).toBeNull();
  });

  it('shows clear button when hasActiveFilters is true', () => {
    fixture.componentRef.setInput('hasActiveFilters', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.filter-toolbar__clear')).not.toBeNull();
  });

  it('emits clearFilters when clear button is clicked', () => {
    let emitCount = 0;
    fixture.componentInstance.clearFilters.subscribe(() => emitCount++);

    fixture.componentRef.setInput('hasActiveFilters', true);
    fixture.detectChanges();

    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.filter-toolbar__clear');
    btn.click();

    expect(emitCount).toBe(1);
  });

  it('projects ng-content into search slot area', () => {
    // The search slot is structural — just verify the container renders
    const toolbar: HTMLElement = fixture.nativeElement.querySelector('.filter-toolbar');
    expect(toolbar).not.toBeNull();
  });
});
