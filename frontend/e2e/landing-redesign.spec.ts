import { test, expect } from '@playwright/test';

const HEADLINE = 'Your important links,';
const HEADLINE_ACCENT = 'beautifully organized.';
const CTA_PRIMARY = 'Register';
const CTA_SECONDARY = 'View demo';
const FEATURE_CARDS = [
  'Save in one click',
  'Organize with tags & categories',
  'Archive without losing links',
  'Find anything fast',
];

test.describe('Landing page redesign — R3', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('desktop: headline, both hero CTAs, and 4 feature cards render', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    await expect(page.getByRole('heading', { level: 1 })).toContainText(HEADLINE);
    await expect(page.getByRole('heading', { level: 1 })).toContainText(HEADLINE_ACCENT);

    await expect(page.getByRole('link', { name: CTA_PRIMARY }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: CTA_SECONDARY })).toBeVisible();

    for (const title of FEATURE_CARDS) {
      await expect(page.getByRole('heading', { name: title })).toBeVisible();
    }
  });

  test('mobile: headline, CTAs, and feature cards stack vertically', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });

    await expect(page.getByRole('heading', { level: 1 })).toContainText(HEADLINE);

    await expect(page.getByRole('link', { name: CTA_PRIMARY }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: CTA_SECONDARY })).toBeVisible();

    for (const title of FEATURE_CARDS) {
      await expect(page.getByRole('heading', { name: title })).toBeVisible();
    }
  });

  test('header shows Sign in and Register, no nav links', async ({ page }) => {
    const header = page.getByRole('banner');
    await expect(header.getByRole('link', { name: 'Sign in' })).toBeVisible();
    await expect(header.getByRole('link', { name: 'Register' })).toBeVisible();
    await expect(header.getByRole('link', { name: 'Features' })).not.toBeVisible();
    await expect(header.getByRole('link', { name: 'How it works' })).not.toBeVisible();
  });

  test('footer shows copyright and no pricing/social columns', async ({ page }) => {
    const footer = page.getByRole('contentinfo');
    await expect(footer).toContainText('Favourites');
    await expect(footer.getByRole('link', { name: 'Sign in' })).toBeVisible();
    await expect(footer).not.toContainText('Go Premium');
    await expect(footer).not.toContainText('Features');
  });
});
