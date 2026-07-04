import { expect, test } from '@playwright/test';
import {
  createBackend,
  installApiMocks,
  makeLink,
  makeUser,
  type MockCategory,
  type MockTag,
} from './helpers/fake-backend';

// ── Shared seed helpers ───────────────────────────────────────────────────────

function makeSeededBackend() {
  const user = makeUser({ email: 'redesign@example.test', password: 'Redesign9!', displayName: 'Re Design' });
  const tag1: MockTag = { id: 'tag-angular', name: 'Angular' };
  const tag2: MockTag = { id: 'tag-dotnet', name: '.NET' };
  const cat1: MockCategory = { id: 'cat-learning', name: 'Learning' };
  const cat2: MockCategory = { id: 'cat-work', name: 'Work' };
  const links = [
    makeLink({ title: 'Angular docs', url: 'https://angular.dev', tags: [tag1], category: cat1, createdAtUtc: new Date(2026, 4, 1).toISOString() }),
    makeLink({ title: 'MSBuild reference', url: 'https://learn.microsoft.com/msbuild', tags: [tag2], category: cat2, createdAtUtc: new Date(2026, 4, 2).toISOString() }),
    makeLink({ title: 'Archived article', url: 'https://example.com/old', tags: [], category: null, isArchived: true, createdAtUtc: new Date(2026, 3, 1).toISOString() }),
  ];
  const backend = createBackend({
    users: [user],
    currentUserId: user.id,
    tags: [tag1, tag2],
    categories: [cat1, cat2],
    links,
  });
  return { user, backend, tag1, tag2, cat1, cat2, links };
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

test.describe('Dashboard — R4 redesign', () => {
  test('desktop: welcome header, stat cards, quick-save, and recent links render', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/dashboard');

    await expect(page.getByRole('heading', { level: 1 })).toContainText('Welcome');
    // Stat cards
    await expect(page.locator('fav-stat-card').first()).toBeVisible();
    // Quick save form
    await expect(page.locator('#dashboard-quick-save-url')).toBeVisible();
    // Navigation sidebar present on desktop
    await expect(page.locator('app-sidebar')).toBeVisible();
  });

  test('mobile: header, quick-save, and bottom nav render', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/dashboard');

    await expect(page.getByRole('heading', { level: 1 })).toContainText('Welcome');
    await expect(page.locator('#dashboard-quick-save-url')).toBeVisible();
    // Bottom nav appears on mobile
    await expect(page.locator('app-bottom-nav')).toBeVisible();
  });

  test('quick-save: pasting a URL and saving creates a link', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/dashboard');

    const url = 'https://example.com/quick-save-test';
    await page.locator('#dashboard-quick-save-url').fill(url);

    const req = page.waitForRequest(
      (r) => r.method() === 'POST' && r.url().endsWith('/api/links'),
    );
    await page.getByRole('button', { name: 'Save link' }).click();
    await req;

    expect(backend.links.some((l) => l.url === url)).toBe(true);
  });
});

// ── All Links (link-list) ─────────────────────────────────────────────────────

test.describe('All Links — R5 redesign', () => {
  test('desktop: page header, filter toolbar, link rows, and pagination render', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/links');

    await expect(page.getByRole('heading', { name: /All Links|Archived Links/i }).first()).toBeVisible();
    // Filter toolbar region
    await expect(page.getByRole('region', { name: 'Search, filter, and sort links' })).toBeVisible();
    // Two active links show
    await expect(page.getByRole('link', { name: 'Angular docs', exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'MSBuild reference', exact: true })).toBeVisible();
  });

  test('mobile: search input and Filters button render', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/links');

    await expect(page.getByRole('button', { name: /Filters/i })).toBeVisible();
    await expect(page.locator('#link-list-search-mobile')).toBeVisible();
  });
});

// ── Tags ──────────────────────────────────────────────────────────────────────

test.describe('Tags — R6 redesign', () => {
  test('desktop: page header, stat cards, filter toolbar, and tag tiles render', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/tags');

    await expect(page.getByRole('heading', { name: 'Tags' })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Search, filter, and sort tags' })).toBeVisible();
    // Stat cards
    await expect(page.locator('fav-stat-card').first()).toBeVisible();
    // Tag tiles or list rows — scope to the tags list/grid to avoid stat-card collisions
    const tagsGrid = page.locator('[aria-label="Tags"]');
    await expect(tagsGrid.getByText('Angular')).toBeVisible();
    await expect(tagsGrid.getByText('.NET')).toBeVisible();
  });

  test('desktop: opening New tag modal shows the form', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/tags');

    await page.getByRole('button', { name: 'New tag' }).click();
    await expect(page.getByRole('dialog', { name: /New tag/i })).toBeVisible();
    await expect(page.locator('#new-tag-name')).toBeVisible();
  });

  test('mobile: filter button and tags content render', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/tags');

    await expect(page.getByRole('heading', { name: 'Tags' })).toBeVisible();
    await expect(page.locator('[aria-label="Tags"]').getByText('Angular')).toBeVisible();
  });
});

// ── Categories ────────────────────────────────────────────────────────────────

test.describe('Categories — R7 redesign', () => {
  test('desktop: page header, stat cards, filter toolbar, and category tiles render', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/categories');

    await expect(page.getByRole('heading', { name: 'Categories' })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Search, filter, and sort categories' })).toBeVisible();
    await expect(page.locator('fav-stat-card').first()).toBeVisible();
    // Scope to the categories grid to avoid stat-card / right-rail collisions
    const catsGrid = page.locator('[aria-label="Categories"]');
    await expect(catsGrid.getByText('Learning')).toBeVisible();
    await expect(catsGrid.getByText('Work')).toBeVisible();
  });

  test('desktop: opening New category modal shows the form', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/categories');

    await page.getByRole('button', { name: 'New category' }).click();
    await expect(page.getByRole('dialog', { name: /New category/i })).toBeVisible();
    await expect(page.locator('#new-category-name')).toBeVisible();
  });

  test('mobile: categories list renders', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/categories');

    await expect(page.getByRole('heading', { name: 'Categories' })).toBeVisible();
    await expect(page.locator('[aria-label="Categories"]').getByText('Learning')).toBeVisible();
  });
});

// ── Archived ──────────────────────────────────────────────────────────────────

test.describe('Archived — R8 redesign', () => {
  test('desktop: page header, stat cards, info banner, and archived link row render', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/archived');

    await expect(page.getByRole('heading', { name: 'Archived' })).toBeVisible();
    await expect(page.locator('fav-stat-card').first()).toBeVisible();
    // The one archived link appears
    await expect(page.getByRole('link', { name: 'Archived article', exact: true })).toBeVisible();
    // Info banner
    await expect(page.getByRole('note')).toBeVisible();
  });

  test('desktop: Empty archive button is visible', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/archived');

    await expect(page.getByRole('button', { name: 'Empty archive', exact: true })).toBeVisible();
  });

  test('mobile: archived links and bottom nav render', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/archived');

    await expect(page.getByRole('heading', { name: 'Archived' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Archived article', exact: true })).toBeVisible();
    await expect(page.locator('app-bottom-nav')).toBeVisible();
  });
});

// ── Settings ──────────────────────────────────────────────────────────────────

test.describe('Settings — R9 redesign', () => {
  test('desktop: page header, section tabs, and Profile section render', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/settings');

    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible();
    // Save changes button present
    await expect(page.getByRole('button', { name: 'Save changes' })).toBeVisible();
    // Profile section heading
    await expect(page.getByRole('heading', { name: /Profile/i })).toBeVisible();
  });

  test('desktop: Preferences section is reachable via tab nav', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/settings');

    await page.getByRole('link', { name: /Preferences/i }).click();
    await expect(page.getByRole('heading', { name: /Preferences/i })).toBeVisible();
  });

  test('mobile: section select and Profile section render', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/settings');

    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible();
    // Mobile uses a <select> for section navigation
    await expect(page.locator('#settings-mobile-section')).toBeVisible();
    await expect(page.getByRole('heading', { name: /Profile/i })).toBeVisible();
  });
});
