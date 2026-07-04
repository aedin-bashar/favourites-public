import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { FavIcons } from '../../icons/fav-icons';

let nextSearchId = 0;

/**
 * Global search input for the top bar.
 * On submit navigates to /app/links?search=<query>.
 * Keyboard shortcut: pressing '/' focuses the input (when focus is not already
 * inside a text field).
 */
@Component({
  selector: 'fav-app-search',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="app-search" (ngSubmit)="onSubmit()">
      <label class="app-search__label visually-hidden" [attr.for]="inputId">
        Search
      </label>
      <i [class]="icons.Search" class="app-search__icon" aria-hidden="true"></i>
      <input
        #searchInput
        [id]="inputId"
        type="search"
        class="app-search__input"
        placeholder="Search by title, URL or description..."
        autocomplete="off"
        [(ngModel)]="query"
        name="q"
      />
      @if (query) {
        <button
          type="button"
          class="app-search__clear"
          aria-label="Clear search"
          (click)="clearSearch()"
        >
          <i [class]="icons.Close" aria-hidden="true"></i>
        </button>
      }
      <button type="submit" class="app-search__btn" aria-label="Search">
        Search
      </button>
    </form>
  `,
  styleUrl: './app-search.component.scss',
})
export class AppSearchComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly icons = FavIcons;
  protected readonly inputId = `global-search-${++nextSearchId}`;
  protected query = '';

  @ViewChild('searchInput') private readonly inputRef?: ElementRef<HTMLInputElement>;

  ngOnInit(): void {
    // Sync topbar query with URL search param on every navigation.
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        // Walk up to the root to read query params regardless of route depth.
        let r = this.route;
        while (r.firstChild) r = r.firstChild;
        this.query = r.snapshot.queryParamMap.get('search')?.trim() ?? '';
      });
  }

  protected onSubmit(): void {
    const q = this.query.trim();
    if (!q) return;
    void this.router.navigate(['/app/links'], { queryParams: { search: q } });
  }

  protected clearSearch(): void {
    this.query = '';
    void this.router.navigate(['/app/links'], { queryParams: {} });
  }

  @HostListener('document:keydown', ['$event'])
  protected onGlobalKeydown(event: KeyboardEvent): void {
    if (event.key !== '/') return;
    const target = event.target as HTMLElement;
    const tag = target.tagName.toLowerCase();
    if (tag === 'input' || tag === 'textarea' || tag === 'select' || target.isContentEditable) {
      return;
    }
    event.preventDefault();
    this.inputRef?.nativeElement.focus();
  }
}
