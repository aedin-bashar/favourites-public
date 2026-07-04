import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  HostListener,
  Input,
  Output,
  inject,
  signal,
} from '@angular/core';
import { FavIcons } from '../../../shared/icons/fav-icons';
import { LinksApiService } from '../services/links-api.service';

type ImportPhase = 'idle' | 'uploading' | 'done' | 'error';

@Component({
  selector: 'app-import-bookmarks-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fav-modal-backdrop" aria-hidden="true" (click)="close()"></div>
    <div class="fav-modal import-modal" role="dialog" aria-modal="true"
         aria-labelledby="import-modal-title"
         (click)="onBackdropClick($event)">
      <div class="fav-modal__dialog import-modal__dialog">
        <header class="fav-modal__header">
          <h2 class="fav-modal__title" id="import-modal-title">
            <i class="fa-solid fa-file-import fav-icon" aria-hidden="true"></i>
            Import bookmarks
          </h2>
          <button type="button" class="fav-modal__close fav-icon-btn"
                  aria-label="Close" (click)="close()">
            <i [class]="icons.Close" aria-hidden="true"></i>
          </button>
        </header>

        <div class="fav-modal__body">

          @if (phase() === 'idle') {
            @if (format === 'json') {
              <p class="import-modal__hint">
                Upload a JSON file exported from Favourites. Tags, categories,
                descriptions and archive status are restored.
                Links already in your library are skipped automatically.
              </p>
            } @else {
              <p class="import-modal__hint">
                Upload an HTML bookmark file exported from your browser
                (Chrome, Firefox, Safari, Edge) — folder names become tags.
                Favourites HTML exports restore categories and tags as-is.
                Links already in your library are skipped automatically.
              </p>
            }

            <div class="import-modal__drop-zone"
                 [class.import-modal__drop-zone--dragover]="dragging()"
                 (dragover)="onDragOver($event)"
                 (dragleave)="dragging.set(false)"
                 (drop)="onDrop($event)"
                 (click)="fileInput.click()">
              <i class="fa-solid fa-upload import-modal__drop-icon" aria-hidden="true"></i>
              <p class="import-modal__drop-text">
                Drag &amp; drop your
                <strong>{{ format === 'json' ? 'favourites-export.json' : 'bookmarks.html' }}</strong>
                here, or <span class="fav-link">click to browse</span>
              </p>
              @if (selectedFile()) {
                <p class="import-modal__selected-file">
                  <i class="fa-solid fa-file fav-icon" aria-hidden="true"></i>
                  {{ selectedFile()!.name }}
                </p>
              }
            </div>

            <input #fileInput type="file" [accept]="format === 'json' ? '.json' : '.html,.htm'"
                   class="fav-sr-only"
                   (change)="onFileChange($event)" />
          }

          @if (phase() === 'uploading') {
            <div class="import-modal__progress">
              <div class="fav-spinner" aria-hidden="true"></div>
              <p>Importing bookmarks, please wait…</p>
            </div>
          }

          @if (phase() === 'done') {
            <div class="import-modal__result import-modal__result--success">
              <i class="fa-solid fa-circle-check import-modal__result-icon" aria-hidden="true"></i>
              <div>
                <p class="import-modal__result-headline">Import complete!</p>
                <p>
                  <strong>{{ result()?.created }}</strong> link(s) added &nbsp;·&nbsp;
                  <strong>{{ result()?.skipped }}</strong> duplicate(s) skipped.
                </p>
              </div>
            </div>
          }

          @if (phase() === 'error') {
            <div class="import-modal__result import-modal__result--error">
              <i [class]="icons.Danger" class="import-modal__result-icon" aria-hidden="true"></i>
              <div>
                <p class="import-modal__result-headline">Import failed</p>
                <p>{{ errorMessage() }}</p>
              </div>
            </div>
          }

        </div>

        <footer class="fav-modal__footer">
          @if (phase() === 'idle') {
            <button type="button" class="fav-btn fav-btn--ghost" (click)="close()">Cancel</button>
            <button type="button" class="fav-btn fav-btn--primary"
                    [disabled]="!selectedFile()"
                    (click)="upload()">
              <i class="fa-solid fa-file-import fav-icon" aria-hidden="true"></i>
              Import
            </button>
          }
          @if (phase() === 'uploading') {
            <button type="button" class="fav-btn fav-btn--ghost" disabled>Cancel</button>
            <button type="button" class="fav-btn fav-btn--primary" disabled>Importing…</button>
          }
          @if (phase() === 'done' || phase() === 'error') {
            @if (phase() === 'error') {
              <button type="button" class="fav-btn fav-btn--ghost" (click)="reset()">Try again</button>
            }
            <button type="button" class="fav-btn fav-btn--primary" (click)="closeWithResult()">Done</button>
          }
        </footer>
      </div>
    </div>
  `,
  styleUrl: './import-bookmarks-modal.component.scss',
})
export class ImportBookmarksModalComponent {
  /** Which file type the modal accepts: browser bookmark HTML or a Favourites JSON export. */
  @Input() format: 'html' | 'json' = 'html';

  protected readonly icons = FavIcons;
  protected readonly phase = signal<ImportPhase>('idle');
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly dragging = signal(false);
  protected readonly result = signal<{ created: number; skipped: number } | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  private readonly linksApi = inject(LinksApiService);

  @Output() readonly closed = new EventEmitter<boolean>();

  close(): void {
    this.closed.emit(false);
  }

  closeWithResult(): void {
    this.closed.emit(this.phase() === 'done');
  }

  reset(): void {
    this.phase.set('idle');
    this.selectedFile.set(null);
    this.result.set(null);
    this.errorMessage.set(null);
  }

  protected onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) {
      this.selectedFile.set(input.files[0]);
    }
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const file = event.dataTransfer?.files[0];
    if (file) this.selectedFile.set(file);
  }

  protected upload(): void {
    const file = this.selectedFile();
    if (!file) return;

    this.phase.set('uploading');

    this.linksApi.importBookmarks(file).subscribe({
      next: (res) => {
        this.result.set(res);
        this.phase.set('done');
      },
      error: (err) => {
        this.errorMessage.set(
          err?.error?.error ?? 'An unexpected error occurred. Please try again.',
        );
        this.phase.set('error');
      },
    });
  }

  protected onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('fav-modal')) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.phase() !== 'uploading') this.close();
  }
}
