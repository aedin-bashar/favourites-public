// `shared/` — reusable components, models, pipes, validators, and design
// helpers used across features. No singletons live here (those go in
// `core/`). Source: docs/ARCHITECTURE.md §8.

export * from './layouts/landing';
export * from './layouts/app-shell';
export { FavIcons } from './icons/fav-icons';
export type { FavIconKey } from './icons/fav-icons';
