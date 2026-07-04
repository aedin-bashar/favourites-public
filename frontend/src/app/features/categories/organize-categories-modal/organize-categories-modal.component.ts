import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  HostListener,
  OnInit,
  Output,
  inject,
  signal,
} from '@angular/core';
import { FavIcons } from '../../../shared/icons/fav-icons';
import { FocusTrapDirective } from '../../../shared/directives/focus-trap.directive';
import {
  CategoriesApiService,
  type CategoryDuplicateGroup,
} from '../services/categories-api.service';

type ModalPhase = 'loading' | 'idle' | 'merging' | 'done' | 'error';

@Component({
  selector: 'app-organize-categories-modal',
  standalone: true,
  imports: [FocusTrapDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fav-modal-backdrop" aria-hidden="true"></div>
    <div
      class="fav-modal org-cat-modal"
      role="dialog"
      aria-modal="true"
      aria-labelledby="org-cat-modal-title"
      (click)="onBackdropClick($event)"
    >
      <div
        class="fav-modal__dialog org-cat-modal__dialog"
        favFocusTrap
        (click)="$event.stopPropagation()"
      >
        <header class="fav-modal__header">
          <h2 class="fav-modal__title" id="org-cat-modal-title">
            <i class="fa-solid fa-folder-tree fav-icon" aria-hidden="true"></i>
            Organize categories
          </h2>
          <button
            type="button"
            class="fav-modal__close fav-icon-btn"
            aria-label="Close"
            [disabled]="phase() === 'merging'"
            (click)="close()"
          >
            <i [class]="icons.Close" aria-hidden="true"></i>
          </button>
        </header>

        <div class="fav-modal__body">
          @if (phase() === 'loading') {
            <div class="org-cat-modal__loading">
              <div class="fav-spinner" aria-hidden="true"></div>
              <span>Scanning for duplicates...</span>
            </div>
          }

          @if (phase() === 'error') {
            <div class="fav-alert fav-alert--danger">
              <i [class]="icons.Danger" class="fav-alert__icon" aria-hidden="true"></i>
              <div class="fav-alert__body">{{ errorMessage() }}</div>
            </div>
          }

          @if (phase() === 'idle' || phase() === 'merging') {
            @if (groups().length === 0) {
              <div class="org-cat-modal__empty">
                <i
                  class="fa-solid fa-circle-check org-cat-modal__empty-icon"
                  aria-hidden="true"
                ></i>
                <h3>No duplicate categories found</h3>
                <p>Your category library looks clean.</p>
              </div>
            } @else {
              <p class="org-cat-modal__hint">
                {{ groups().length }} duplicate group(s) found.
                Select which category to keep; the others and their links will be merged into it.
              </p>
              @for (group of groups(); track group; let gi = $index) {
                <div class="org-cat-modal__group">
                  <p class="org-cat-modal__group-label">Group {{ gi + 1 }}</p>
                  <div class="org-cat-modal__categories">
                    @for (cat of group.categories; track cat.id) {
                      <button
                        type="button"
                        class="org-cat-modal__cat"
                        [class.org-cat-modal__cat--selected]="selectedKeep()[gi] === cat.id"
                        (click)="selectKeep(gi, cat.id)"
                      >
                        <span class="org-cat-modal__cat-name">{{ cat.name }}</span>
                        <span class="org-cat-modal__cat-count">{{ cat.linkCount }} link(s)</span>
                        @if (selectedKeep()[gi] === cat.id) {
                          <i
                            class="fa-solid fa-check org-cat-modal__cat-check"
                            aria-hidden="true"
                          ></i>
                        }
                      </button>
                    }
                  </div>
                </div>
              }
            }
          }

          @if (phase() === 'done') {
            <div class="org-cat-modal__done">
              <i
                class="fa-solid fa-circle-check org-cat-modal__done-icon"
                aria-hidden="true"
              ></i>
              <p><strong>{{ mergedCount() }}</strong> category(ies) merged successfully.</p>
            </div>
          }
        </div>

        <footer class="fav-modal__footer">
          @if (phase() === 'idle') {
            @if (groups().length === 0) {
              <button type="button" class="fav-btn fav-btn--primary" (click)="close()">Close</button>
            } @else {
              <button type="button" class="fav-btn fav-btn--ghost" (click)="close()">Cancel</button>
              <button
                type="button"
                class="fav-btn fav-btn--primary"
                [disabled]="!allGroupsHaveSelection()"
                (click)="mergeAll()"
              >
                <i class="fa-solid fa-code-merge fav-icon" aria-hidden="true"></i>
                Merge selected
              </button>
            }
          }
          @if (phase() === 'merging') {
            <button type="button" class="fav-btn fav-btn--ghost" disabled>Cancel</button>
            <button type="button" class="fav-btn fav-btn--primary" disabled>Merging...</button>
          }
          @if (phase() === 'done' || phase() === 'error') {
            <button type="button" class="fav-btn fav-btn--primary" (click)="closeWithResult()">
              Done
            </button>
          }
        </footer>
      </div>
    </div>
  `,
  styleUrl: './organize-categories-modal.component.scss',
})
export class OrganizeCategoriesModalComponent implements OnInit {
  protected readonly icons = FavIcons;
  protected readonly phase = signal<ModalPhase>('loading');
  protected readonly groups = signal<CategoryDuplicateGroup[]>([]);
  protected readonly selectedKeep = signal<(string | null)[]>([]);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly mergedCount = signal(0);

  private readonly categoriesApi = inject(CategoriesApiService);

  @Output() readonly closed = new EventEmitter<boolean>();

  ngOnInit(): void {
    this.categoriesApi.duplicates().subscribe({
      next: (groups) => {
        this.groups.set(groups);
        this.selectedKeep.set(groups.map(() => null));
        this.phase.set('idle');
      },
      error: () => {
        this.errorMessage.set('Failed to load duplicate categories. Please try again.');
        this.phase.set('error');
      },
    });
  }

  protected selectKeep(groupIndex: number, catId: string): void {
    const current = [...this.selectedKeep()];
    current[groupIndex] = catId;
    this.selectedKeep.set(current);
  }

  protected allGroupsHaveSelection(): boolean {
    return this.selectedKeep().every((id) => id !== null);
  }

  protected mergeAll(): void {
    const groups = this.groups();
    const keeps = this.selectedKeep();
    if (!this.allGroupsHaveSelection()) return;

    this.phase.set('merging');

    const merges = groups.map((group, i) => ({
      keepCategoryId: keeps[i]!,
      mergeCategoryIds: group.categories.filter((c) => c.id !== keeps[i]).map((c) => c.id),
    }));

    let total = 0;
    const run = (index: number): void => {
      if (index >= merges.length) {
        this.mergedCount.set(total);
        this.phase.set('done');
        return;
      }
      const { keepCategoryId, mergeCategoryIds } = merges[index];
      this.categoriesApi.merge(keepCategoryId, mergeCategoryIds).subscribe({
        next: (res) => {
          total += res.merged;
          run(index + 1);
        },
        error: () => {
          this.errorMessage.set('One or more merges failed.');
          this.phase.set('error');
        },
      });
    };
    run(0);
  }

  close(): void {
    if (this.phase() !== 'merging') {
      this.closed.emit(false);
    }
  }

  closeWithResult(): void {
    this.closed.emit(this.phase() === 'done');
  }

  protected onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('fav-modal')) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.close();
  }
}
