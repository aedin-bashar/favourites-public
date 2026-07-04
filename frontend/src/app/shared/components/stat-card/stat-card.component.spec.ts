import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatCardComponent } from './stat-card.component';

describe('StatCardComponent', () => {
  let fixture: ComponentFixture<StatCardComponent>;
  let component: StatCardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(StatCardComponent);
    component = fixture.componentInstance;
    component.label = 'Total links';
    component.value = 42;
    component.icon = 'fa-link';
    component.tone = 'teal';
    fixture.detectChanges();
  });

  it('renders the value', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.stat-card__value')?.textContent?.trim()).toBe('42');
  });

  it('renders the label', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.stat-card__label')?.textContent?.trim()).toBe('Total links');
  });

  it('applies the tone as a data attribute', () => {
    const card: HTMLElement = fixture.nativeElement.querySelector('.stat-card');
    expect(card.getAttribute('data-tone')).toBe('teal');
  });

  it('defaults tone to teal', () => {
    const fresh = TestBed.createComponent(StatCardComponent);
    fresh.componentInstance.label = 'x';
    fresh.componentInstance.value = 0;
    fresh.componentInstance.icon = 'fa-tag';
    fresh.detectChanges();
    expect(fresh.componentInstance.tone).toBe('teal');
  });
});
