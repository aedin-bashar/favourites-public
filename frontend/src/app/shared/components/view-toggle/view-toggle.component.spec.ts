import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ViewToggleComponent } from './view-toggle.component';
import { ViewModeService } from './view-mode.service';

describe('ViewToggleComponent', () => {
  let fixture: ComponentFixture<ViewToggleComponent>;
  let component: ViewToggleComponent;
  let service: ViewModeService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ViewToggleComponent],
    }).compileComponents();

    service = TestBed.inject(ViewModeService);
    vi.spyOn(service, 'get').mockReturnValue('list');
    vi.spyOn(service, 'set');

    fixture = TestBed.createComponent(ViewToggleComponent);
    component = fixture.componentInstance;
    component.pageId = 'links';
    fixture.detectChanges();
  });

  it('initialises from ViewModeService', () => {
    expect(service.get).toHaveBeenCalledWith('links');
    expect(component.current).toBe('list');
  });

  it('activates list button by default', () => {
    const btns = fixture.nativeElement.querySelectorAll('.view-toggle__btn');
    expect(btns[0].classList).toContain('view-toggle__btn--active');
    expect(btns[1].classList).not.toContain('view-toggle__btn--active');
  });

  it('switches to cards on button click', () => {
    const emitted: string[] = [];
    component.modeChange.subscribe((m: string) => emitted.push(m));

    const btns: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('.view-toggle__btn');
    btns[1].click();
    fixture.detectChanges();

    expect(component.current).toBe('cards');
    expect(service.set).toHaveBeenCalledWith('links', 'cards');
    expect(emitted).toEqual(['cards']);
  });

  it('does not emit when clicking the already-active button', () => {
    const emitted: string[] = [];
    component.modeChange.subscribe((m: string) => emitted.push(m));

    const btns: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('.view-toggle__btn');
    btns[0].click();

    expect(emitted).toEqual([]);
  });
});
