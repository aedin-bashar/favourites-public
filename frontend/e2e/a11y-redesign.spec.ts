import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import {
  createBackend,
  installApiMocks,
  makeLink,
  makeUser,
  type MockCategory,
  type MockTag,
} from './helpers/fake-backend';

function makeSeededBackend() {
  const user = makeUser({ email: 'a11y@example.test', password: 'A11yTest9!', displayName: 'A11y User' });
  const tag1: MockTag = { id: 'tag-a11y-1', name: 'Accessibility' };
  const cat1: MockCategory = { id: 'cat-a11y-1', name: 'Research' };
  const links = [
    makeLink({ title: 'WCAG Guidelines', url: 'https://wcag.example.com', tags: [tag1], category: cat1 }),
    makeLink({ title: 'Archived item', url: 'https://archived.example.com', tags: [], category: null, isArchived: true }),
  ];
  const backend = createBackend({
    users: [user],
    currentUserId: user.id,
    tags: [tag1],
    categories: [cat1],
    links,
  });
  return { user, backend };
}

test.describe('Accessibility audit — redesigned pages', () => {
  test('Landing page: no critical axe violations', async ({ page }) => {
    await page.goto('/');
    await page.setViewportSize({ width: 1440, height: 900 });

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Dashboard: no critical axe violations', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/dashboard');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('All Links: no critical axe violations', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/links');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Tags: no critical axe violations', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/tags');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Categories: no critical axe violations', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/categories');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Archived: no critical axe violations', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/archived');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Settings: no critical axe violations', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const { backend } = makeSeededBackend();
    await installApiMocks(page, backend);
    await page.goto('/app/settings');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

function formatViolations(violations: import('@axe-core/playwright').AxeResults['violations']): string {
  if (violations.length === 0) return '';
  return violations
    .map((v) => `[${v.impact}] ${v.id}: ${v.description}\n  ${v.nodes.map((n) => n.html).join('\n  ')}`)
    .join('\n\n');
}
