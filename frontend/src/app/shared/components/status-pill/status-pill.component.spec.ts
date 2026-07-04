import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatusPillComponent } from './status-pill.component';

describe('StatusPillComponent', () => {
  let fixture: ComponentFixture<StatusPillComponent>;
  let component: StatusPillComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatusPillComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(StatusPillComponent);
    component = fixture.componentInstance;
  });

  it('renders the Active label and active modifier class', () => {
    component.status = 'active';
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement.querySelector('.status-pill');
    expect(el).toBeTruthy();
    expect(el.classList).toContain('status-pill--active');
    expect(el.textContent?.trim()).toBe('Active');
  });

  it('renders the Archived label and archived modifier class', () => {
    component.status = 'archived';
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement.querySelector('.status-pill');
    expect(el.classList).toContain('status-pill--archived');
    expect(el.textContent?.trim()).toBe('Archived');
  });

  it('handles a custom status value', () => {
    component.status = 'draft';
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement.querySelector('.status-pill');
    expect(el.classList).toContain('status-pill--draft');
    expect(el.textContent?.trim()).toBe('Draft');
  });
});
