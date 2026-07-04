import {
  Directive,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  inject,
} from '@angular/core';

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

@Directive({ selector: '[favFocusTrap]', standalone: true })
export class FocusTrapDirective implements OnInit, OnDestroy {
  private readonly el = inject(ElementRef<HTMLElement>);
  private previousFocus: HTMLElement | null = null;

  ngOnInit(): void {
    this.previousFocus = document.activeElement as HTMLElement;
    const first = this.focusable()[0];
    if (first) {
      first.focus();
    }
  }

  ngOnDestroy(): void {
    this.previousFocus?.focus();
  }

  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const items = this.focusable();
    if (items.length === 0) return;
    const first = items[0];
    const last = items[items.length - 1];
    if (event.shiftKey) {
      if (document.activeElement === first) {
        event.preventDefault();
        last.focus();
      }
    } else {
      if (document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }
  }

  private focusable(): HTMLElement[] {
    const nodes = this.el.nativeElement.querySelectorAll(FOCUSABLE);
    return (Array.from(nodes) as HTMLElement[]).filter(
      (el) => !el.closest('[hidden]'),
    );
  }
}
