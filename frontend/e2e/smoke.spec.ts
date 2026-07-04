import { expect, test } from '@playwright/test';

test('app loads at root and reaches the landing page', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/favourites/i);
  await expect(page.locator('app-root')).toBeVisible();
});
