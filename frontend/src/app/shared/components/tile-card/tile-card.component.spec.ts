import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { TileCardComponent, TileCardMenuItem } from './tile-card.component';

describe('TileCardComponent', () => {
  let fixture: ComponentFixture<TileCardComponent>;
  let component: TileCardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TileCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TileCardComponent);
    component = fixture.componentInstance;
    component.icon = 'fa-tag';
    component.name = 'Angular';
    component.subtitle = '12 links';
    // detectChanges called per-test after all inputs are set
  });

  it('renders the name and subtitle', () => {
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.tile-card__name')?.textContent?.trim()).toBe('Angular');
    expect(el.querySelector('.tile-card__subtitle')?.textContent?.trim()).toBe('12 links');
  });

  it('applies the tone data attribute', () => {
    component.tone = 'indigo';
    fixture.detectChanges();
    const card: HTMLElement = fixture.nativeElement.querySelector('.tile-card');
    expect(card.getAttribute('data-tone')).toBe('indigo');
  });

  it('shows the action label and emits on click', () => {
    component.actionLabel = 'Open links';
    fixture.detectChanges();
    const spy = vi.fn();
    component.action.subscribe(spy);
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.tile-card__action-link');
    expect(btn).toBeTruthy();
    btn.click();
    expect(spy).toHaveBeenCalled();
  });

  it('hides the action section when actionLabel is empty', () => {
    fixture.detectChanges();
    const action = fixture.nativeElement.querySelector('.tile-card__action');
    expect(action).toBeNull();
  });

  it('emits menuAction with the clicked item', () => {
    const items: TileCardMenuItem[] = [{ label: 'Rename' }, { label: 'Delete', danger: true }];
    component.menuItems = items;
    fixture.detectChanges();
    const spy = vi.fn();
    component.menuAction.subscribe(spy);
    const btns: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('.dropdown-item');
    btns[0].click();
    expect(spy).toHaveBeenCalledWith(items[0]);
  });
});
