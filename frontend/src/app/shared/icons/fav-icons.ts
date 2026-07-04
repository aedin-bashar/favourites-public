// Centralized Font Awesome Free icon vocabulary for Favourites.
// Source mapping: docs/UI_DESIGN_GUIDE.md §17.
//
// Use these constants in templates instead of hard-coding `fa-*` class
// strings. This keeps the icon set consistent across the app and gives
// us a single place to swap an icon if the design guide changes.
//
// Each value is the full class string ready to drop into an <i>:
//   <i [class]="FavIcons.SaveLink" aria-hidden="true"></i>
//
// All icons are from Font Awesome Free (solid or regular). No Pro variants.

export const FavIcons = {
  // Primary actions
  SaveLink:   'fa-solid fa-bookmark',
  AddLink:    'fa-solid fa-plus',
  Link:       'fa-solid fa-link',
  Search:     'fa-solid fa-magnifying-glass',

  // Domain objects
  Tag:        'fa-solid fa-tag',
  Category:   'fa-solid fa-folder',

  // Navigation
  Dashboard:  'fa-solid fa-house',
  DashboardAlt: 'fa-solid fa-table-cells-large',
  AllLinks:   'fa-solid fa-bookmark',
  Tags:       'fa-solid fa-tag',
  Categories: 'fa-solid fa-folder',
  Archived:   'fa-solid fa-box-archive',
  Settings:   'fa-solid fa-gear',
  More:       'fa-solid fa-ellipsis',

  // Item actions
  OpenLink:   'fa-solid fa-arrow-up-right-from-square',
  Edit:       'fa-solid fa-pen',
  Delete:     'fa-solid fa-trash',
  Archive:    'fa-solid fa-box-archive',
  Restore:    'fa-solid fa-arrow-rotate-left',

  // Trust / account
  Security:   'fa-solid fa-shield-halved',
  Lock:       'fa-solid fa-lock',
  User:       'fa-solid fa-circle-user',
  SignOut:    'fa-solid fa-right-from-bracket',
  EyeShow:    'fa-solid fa-eye',
  EyeHide:    'fa-solid fa-eye-slash',

  // UI chrome
  Menu:        'fa-solid fa-bars',
  Close:       'fa-solid fa-xmark',
  Check:       'fa-solid fa-check',
  ChevronDown: 'fa-solid fa-chevron-down',
  ChevronRight:'fa-solid fa-chevron-right',
  Bell:        'fa-solid fa-bell',
  CaretDown:   'fa-solid fa-caret-down',

  // State / status
  Info:       'fa-solid fa-circle-info',
  Warning:    'fa-solid fa-triangle-exclamation',
  Danger:     'fa-solid fa-circle-exclamation',
  Success:    'fa-solid fa-circle-check',

  // Empty states / decorative
  EmptyBox:   'fa-regular fa-folder-open',
  Heart:      'fa-solid fa-heart',
} as const;

export type FavIconKey = keyof typeof FavIcons;
