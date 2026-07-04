export type ThemeMode = 'light' | 'dark' | 'system';
export type DensityMode = 'comfortable' | 'compact';
export type TagsDefaultSort = 'name' | 'most-used' | 'newest';
export type CategoriesDefaultSort = 'name' | 'largest' | 'newest' | 'recently-active';

export interface UserPreferences {
  readonly theme: ThemeMode;
  readonly density: DensityMode;
  readonly defaultCategoryId: string | null;
  readonly autoExtractTitle: boolean;
  readonly showFavicon: boolean;
  readonly openInNewTab: boolean;
  readonly confirmBeforeDelete: boolean;
  readonly suggestTagsAutomatically: boolean;
  readonly showColorsOnTagChips: boolean;
  readonly tagsDefaultSort: TagsDefaultSort;
  readonly categoriesDefaultSort: CategoriesDefaultSort;
  readonly weeklySummaryEmail: boolean;
  readonly securityAlerts: boolean;
  readonly productUpdates: boolean;
  readonly updatedAtUtc: string;
}

export type PatchUserPreferencesRequest = Omit<UserPreferences, 'updatedAtUtc'>;

export interface UpdateProfileRequest {
  readonly displayName: string;
}

export interface UserProfileResponse {
  readonly id: string;
  readonly displayName: string;
  readonly email: string;
}

export const defaultUserPreferences: UserPreferences = {
  theme: 'light',
  density: 'comfortable',
  defaultCategoryId: null,
  autoExtractTitle: true,
  showFavicon: true,
  openInNewTab: true,
  confirmBeforeDelete: true,
  suggestTagsAutomatically: true,
  showColorsOnTagChips: true,
  tagsDefaultSort: 'name',
  categoriesDefaultSort: 'name',
  weeklySummaryEmail: false,
  securityAlerts: true,
  productUpdates: false,
  updatedAtUtc: '',
};
