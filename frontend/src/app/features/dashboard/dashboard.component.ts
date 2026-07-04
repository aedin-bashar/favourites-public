import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  type AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  type ValidationErrors,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { UserPreferencesService } from '../settings/services/user-preferences.service';
import { FavIcons } from '../../shared/icons/fav-icons';
import { FaviconUrlPipe } from '../../shared/pipes/favicon-url.pipe';
import { RailWidgetComponent } from '../../shared/components/right-rail/rail-widget.component';
import { RightRailComponent } from '../../shared/components/right-rail/right-rail.component';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import type { CategoryResponse } from '../categories/models/category.models';
import { CategoriesApiService } from '../categories/services/categories-api.service';
import type { CreateLinkRequest, LinkResponse } from '../links/models/link.models';
import { LinksApiService } from '../links/services/links-api.service';
import { deriveTitleFromUrl } from '../links/utils/derive-title-from-url';
import type { TagResponse } from '../tags/models/tag.models';
import { TagsApiService } from '../tags/services/tags-api.service';
import type { DashboardSummary } from './services/dashboard-api.service';
import { DashboardApiService } from './services/dashboard-api.service';

const RECENT_LINKS_LIMIT = 6;
const COMMON_TAGS_LIMIT = 8;
const TOP_CATEGORIES_LIMIT = 6;

interface CountedItem {
  readonly id: string;
  readonly name: string;
  readonly color?: string;
  readonly count: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    FaviconUrlPipe,
    ReactiveFormsModule,
    RouterLink,
    StatCardComponent,
    RightRailComponent,
    RailWidgetComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly linksApi = inject(LinksApiService);
  private readonly tagsApi = inject(TagsApiService);
  private readonly categoriesApi = inject(CategoriesApiService);
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly preferencesService = inject(UserPreferencesService);

  protected readonly icons = FavIcons;
  protected readonly preferences = this.preferencesService.preferences;
  protected readonly displayName = computed(() => this.auth.user()?.displayName ?? null);

  // Quick-add form
  protected readonly quickAddForm = new FormGroup({
    url: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(2048), httpUrlValidator],
    }),
  });
  protected readonly quickAddSubmitting = signal(false);
  protected readonly quickAddError = signal<string | null>(null);
  protected readonly quickAddSuccess = signal<LinkResponse | null>(null);

  // Recent links
  protected readonly links = signal<LinkResponse[]>([]);
  protected readonly linksLoading = signal(true);
  protected readonly linksError = signal<string | null>(null);

  // Tags (for rail widgets)
  protected readonly tags = signal<TagResponse[]>([]);
  protected readonly tagsLoading = signal(true);

  // Categories (for rail widgets)
  protected readonly categories = signal<CategoryResponse[]>([]);
  protected readonly categoriesLoading = signal(true);

  // Dashboard summary from backend
  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly summaryLoading = signal(true);
  protected readonly summaryError = signal<string | null>(null);

  protected readonly recentLinks = computed(() => this.links().slice(0, RECENT_LINKS_LIMIT));

  protected readonly commonTags = computed<CountedItem[]>(() => {
    const counts = new Map<string, CountedItem>();
    for (const link of this.links()) {
      for (const tag of link.tags) {
        const existing = counts.get(tag.id);
        counts.set(tag.id, {
          id: tag.id,
          name: tag.name,
          count: (existing?.count ?? 0) + 1,
        });
      }
    }
    for (const tag of this.tags()) {
      if (!counts.has(tag.id)) {
        counts.set(tag.id, { id: tag.id, name: tag.name, count: 0 });
      }
    }
    return [...counts.values()]
      .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name))
      .slice(0, COMMON_TAGS_LIMIT);
  });

  protected readonly topCategories = computed<CountedItem[]>(() => {
    const counts = new Map<string, CountedItem>();
    for (const link of this.links()) {
      const category = link.category;
      if (!category) continue;
      const existing = counts.get(category.id);
      counts.set(category.id, {
        id: category.id,
        name: category.name,
        color: category.color,
        count: (existing?.count ?? 0) + 1,
      });
    }
    for (const category of this.categories()) {
      if (!counts.has(category.id)) {
        counts.set(category.id, { id: category.id, name: category.name, color: category.color, count: 0 });
      }
    }
    return [...counts.values()]
      .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name))
      .slice(0, TOP_CATEGORIES_LIMIT);
  });

  // Stat-card values — prefer summary endpoint; fall back to 0 while loading
  protected readonly totalLinks = computed(() => this.summary()?.totalLinks ?? 0);
  protected readonly totalTags = computed(() => this.summary()?.totalTags ?? 0);
  protected readonly totalCategories = computed(() => this.summary()?.totalCategories ?? 0);
  protected readonly totalArchived = computed(() => this.summary()?.totalArchived ?? 0);
  protected readonly thisWeek = computed(() => this.summary()?.thisWeek ?? null);

  ngOnInit(): void {
    this.loadSummary();
    this.loadLinks();
    this.loadTags();
    this.loadCategories();
  }

  protected onQuickAddSubmit(): void {
    this.quickAddError.set(null);
    if (this.quickAddForm.invalid || this.quickAddSubmitting()) {
      this.quickAddForm.markAllAsTouched();
      return;
    }

    const url = this.quickAddForm.controls.url.value.trim();
    // Link defaults from Settings: "Suggest title from URL" gates the URL-derived
    // title suggestion; "Default category" pre-assigns quick-saved links.
    const prefs = this.preferences();
    const payload: CreateLinkRequest = {
      url,
      title: prefs.autoExtractTitle ? deriveTitleFromUrl(url) || url : url,
      description: null,
      tagIds: [],
      categoryId: prefs.defaultCategoryId,
    };

    this.quickAddSubmitting.set(true);
    this.quickAddSuccess.set(null);
    this.linksApi
      .create(payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (link) => {
          this.quickAddSubmitting.set(false);
          this.quickAddForm.reset();
          this.quickAddSuccess.set(link);
          this.links.update((current) => [link, ...current]);
          // Refresh summary counts after saving a new link
          this.loadSummary();
        },
        error: (err: unknown) => {
          this.quickAddSubmitting.set(false);
          this.quickAddError.set(formatQuickAddError(err));
        },
      });
  }

  protected dismissQuickAddSuccess(): void {
    this.quickAddSuccess.set(null);
  }

  protected reloadLinks(): void {
    this.loadLinks();
  }

  protected trackLinkById(_index: number, link: LinkResponse): string {
    return link.id;
  }

  protected trackCountedById(_index: number, item: CountedItem): string {
    return item.id;
  }

  protected domainOf(url: string): string {
    try {
      return new URL(url).hostname;
    } catch {
      return url;
    }
  }

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.summaryError.set(null);
    this.dashboardApi
      .summary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (summary) => {
          this.summary.set(summary);
          this.summaryLoading.set(false);
        },
        error: () => {
          this.summaryError.set('Could not load summary counts.');
          this.summaryLoading.set(false);
        },
      });
  }

  private loadLinks(): void {
    this.linksLoading.set(true);
    this.linksError.set(null);
    this.linksApi
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (links) => {
          this.links.set(links);
          this.linksLoading.set(false);
        },
        error: (err: unknown) => {
          this.linksError.set(formatLinksError(err));
          this.linksLoading.set(false);
        },
      });
  }

  private loadTags(): void {
    this.tagsLoading.set(true);
    this.tagsApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (tags) => {
          this.tags.set(tags);
          this.tagsLoading.set(false);
        },
        error: () => {
          this.tags.set([]);
          this.tagsLoading.set(false);
        },
      });
  }

  private loadCategories(): void {
    this.categoriesLoading.set(true);
    this.categoriesApi
      .listAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => {
          this.categories.set(categories);
          this.categoriesLoading.set(false);
        },
        error: () => {
          this.categories.set([]);
          this.categoriesLoading.set(false);
        },
      });
  }
}

function httpUrlValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value as string | null | undefined)?.trim();
  if (!value) return null;
  let parsed: URL;
  try {
    parsed = new URL(value);
  } catch {
    return { url: true };
  }
  return parsed.protocol === 'http:' || parsed.protocol === 'https:'
    ? null
    : { url: true };
}

function formatQuickAddError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 400) return 'That URL could not be saved. Please check it and try again.';
    if (status === 401) return 'Your session has expired. Sign in again to save this link.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while saving the link. Please try again.';
}

function formatLinksError(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'status' in err) {
    const status = (err as { status: number }).status;
    if (status === 401) return 'Your session has expired. Sign in again to see your recent links.';
    if (status === 0) return 'Could not reach the server. Check your connection and try again.';
  }
  return 'Something went wrong while loading your recent links. Please try again.';
}
