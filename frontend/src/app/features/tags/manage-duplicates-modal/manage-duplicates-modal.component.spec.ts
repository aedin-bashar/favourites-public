import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ManageDuplicatesModalComponent } from './manage-duplicates-modal.component';
import { TagsApiService } from '../services/tags-api.service';

function modalText(fixture: { nativeElement: HTMLElement }): string {
  return fixture.nativeElement.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

describe('ManageDuplicatesModalComponent', () => {
  it('renders the empty state inside the standard modal dialog shell', async () => {
    await TestBed.configureTestingModule({
      imports: [ManageDuplicatesModalComponent],
      providers: [
        {
          provide: TagsApiService,
          useValue: {
            duplicates: vi.fn(() => of([])),
            merge: vi.fn(),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageDuplicatesModalComponent);
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement | null;
    const dialogCard = fixture.nativeElement.querySelector('.fav-modal__dialog');
    const text = modalText(fixture);

    expect(dialog).not.toBeNull();
    expect(dialogCard).not.toBeNull();
    expect(text).toContain('Manage duplicate tags');
    expect(text).toContain('No duplicate tags found');
    expect(text).toContain('Your tag library looks clean.');
    expect(text).toContain('Close');
    expect(text).not.toContain('Cancel');
  });
});
