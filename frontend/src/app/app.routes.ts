import type { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';

/**
 * Root route table for the Favourites application.
 *
 * Authenticated routes sit under `/app` and are gated by `authGuard`;
 * auth pages use `guestGuard` to bounce signed-in users back to `/app`.
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./features/landing/landing.component').then((m) => m.LandingComponent),
    title: 'Favourites - your important links in one place',
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
    title: 'Sign in - Favourites',
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
    title: 'Create account - Favourites',
  },
  {
    path: 'forgot-password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent,
      ),
    title: 'Forgot password - Favourites',
  },
  {
    path: 'reset-password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/reset-password/reset-password.component').then(
        (m) => m.ResetPasswordComponent,
      ),
    title: 'Reset password - Favourites',
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/app-layout/app-layout.component').then((m) => m.AppLayoutComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
        title: 'Dashboard - Favourites',
      },
      {
        path: 'links',
        loadComponent: () =>
          import('./features/links/link-list/link-list.component').then((m) => m.LinkListComponent),
        title: 'All Links - Favourites',
      },
      {
        path: 'links/:id',
        loadComponent: () =>
          import('./features/links/link-details/link-details.component').then(
            (m) => m.LinkDetailsComponent,
          ),
        title: 'Link details - Favourites',
      },
      {
        path: 'tags',
        loadComponent: () =>
          import('./features/tags/tags-page/tags-page.component').then(
            (m) => m.TagsPageComponent,
          ),
        title: 'Tags - Favourites',
      },
      {
        path: 'categories',
        loadComponent: () =>
          import('./features/categories/categories-page/categories-page.component').then(
            (m) => m.CategoriesPageComponent,
          ),
        title: 'Categories - Favourites',
      },
      {
        path: 'archived',
        loadComponent: () =>
          import('./features/archived/archived-page.component').then(
            (m) => m.ArchivedPageComponent,
          ),
        title: 'Archived - Favourites',
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings-page/settings-page.component').then(
            (m) => m.SettingsPageComponent,
          ),
        title: 'Settings - Favourites',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
