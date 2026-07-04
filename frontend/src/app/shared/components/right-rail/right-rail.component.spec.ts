import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RightRailComponent } from './right-rail.component';
import { RailWidgetComponent } from './rail-widget.component';
import { Component } from '@angular/core';

@Component({
  standalone: true,
  imports: [RightRailComponent, RailWidgetComponent],
  template: `
    <fav-right-rail>
      <fav-rail-widget title="COMMON TAGS">
        <span>chip1</span>
      </fav-rail-widget>
    </fav-right-rail>
  `,
})
class TestHostComponent {}

describe('RightRailComponent', () => {
  let fixture: ComponentFixture<TestHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();
  });

  it('renders the right-rail container', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.right-rail')).not.toBeNull();
  });

  it('renders a rail-widget with the given title', () => {
    const el: HTMLElement = fixture.nativeElement;
    const title = el.querySelector('.rail-widget__title');
    expect(title?.textContent?.trim()).toBe('COMMON TAGS');
  });

  it('projects widget body content', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.rail-widget__body')?.textContent).toContain('chip1');
  });
});
