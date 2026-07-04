import { FavIcons } from '../../icons/fav-icons';

/**
 * Primary authenticated navigation items.
 * Order matches docs/UI_DESIGN_GUIDE.md §9: Dashboard, All Links, Tags,
 * Categories, Archived, Settings.
 *
 * The same array drives both the desktop sidebar and the mobile bottom nav,
 * with the mobile nav picking a subset (Dashboard, Links, Add, Tags, More).
 */
export interface AppNavItem {
  readonly id: string;
  readonly label: string;
  readonly icon: string;
  readonly route: string;
}

export const APP_NAV_ITEMS: readonly AppNavItem[] = [
  { id: 'dashboard',  label: 'Dashboard',  icon: FavIcons.Dashboard, route: '/app/dashboard' },
  { id: 'links',      label: 'All Links',  icon: FavIcons.AllLinks,  route: '/app/links' },
  { id: 'tags',       label: 'Tags',       icon: FavIcons.Tags,      route: '/app/tags' },
  { id: 'categories', label: 'Categories', icon: FavIcons.Categories, route: '/app/categories' },
  { id: 'archived',   label: 'Archived',   icon: FavIcons.Archived,  route: '/app/archived' },
  { id: 'settings',   label: 'Settings',   icon: FavIcons.Settings,  route: '/app/settings' },
] as const;

/**
 * Mobile bottom nav slots — UI guide §8 specifies:
 * Dashboard, Links, Add action, Tags, More.
 * 'Add' is a special centred action button; 'More' opens an overflow sheet.
 */
export interface MobileNavSlot {
  readonly id: 'dashboard' | 'links' | 'add' | 'tags' | 'more';
  readonly label: string;
  readonly icon: string;
  readonly route?: string;
  readonly isAction?: boolean;
}

export const MOBILE_NAV_SLOTS: readonly MobileNavSlot[] = [
  { id: 'dashboard', label: 'Dashboard', icon: FavIcons.Dashboard, route: '/app/dashboard' },
  { id: 'links',     label: 'Links',     icon: FavIcons.AllLinks,  route: '/app/links' },
  { id: 'add',       label: 'Add',       icon: FavIcons.AddLink,   isAction: true },
  { id: 'tags',      label: 'Tags',      icon: FavIcons.Tags,      route: '/app/tags' },
  { id: 'more',      label: 'More',      icon: FavIcons.More },
] as const;
