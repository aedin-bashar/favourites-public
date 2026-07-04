import { expect, test } from '@playwright/test';
import { createBackend, installApiMocks, makeLink, makeUser } from './helpers/fake-backend';

/**
 * Task 2.106 — end-to-end test for searching links.
 *
 * Seeds three links with distinct titles, then types into the All Links
 * search box and asserts that:
 *   - the debounced GET /api/links?search=… fires with the trimmed query;
 *   - only the matching card remains visible;
 *   - the clear (×) button restores the full list.
 *
 * Card title links are matched with `exact: true` because each card also
 * exposes a sibling icon-only "Open <title> in a new tab" anchor whose
 * accessible name would otherwise collide.
 */
test('typing into the search box filters the link list', async ({ page }) => {
  const user = makeUser({ email: 'searcher@example.test', password: 'SearchPass9!' });
  const backend = createBackend({
    users: [user],
    currentUserId: user.id,
    links: [
      makeLink({
        title: 'Angular signals deep dive',
        url: 'https://angular.dev/guide/signals',
        createdAtUtc: new Date(2026, 4, 18, 9, 0, 0).toISOString(),
      }),
      makeLink({
        title: 'Playwright tracing tips',
        url: 'https://playwright.dev/docs/trace-viewer',
        createdAtUtc: new Date(2026, 4, 17, 9, 0, 0).toISOString(),
      }),
      makeLink({
        title: 'Bootstrap utilities cheat sheet',
        url: 'https://getbootstrap.com/docs/5.3/utilities/api/',
        createdAtUtc: new Date(2026, 4, 16, 9, 0, 0).toISOString(),
      }),
    ],
  });
  await installApiMocks(page, backend);

  await page.goto('/app/links');
  await expect(
    page.getByRole('link', { name: 'Angular signals deep dive', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Playwright tracing tips', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Bootstrap utilities cheat sheet', exact: true }),
  ).toBeVisible();

  const searchRequest = page.waitForRequest(
    (req) => req.method() === 'GET' && /\/api\/links\?.*search=playwright/i.test(req.url()),
  );

  await page.getByPlaceholder('Search by title, description, or URL').fill('playwright');

  const sent = await searchRequest;
  expect(new URL(sent.url()).searchParams.get('search')).toBe('playwright');

  await expect(
    page.getByRole('link', { name: 'Playwright tracing tips', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Angular signals deep dive', exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole('link', { name: 'Bootstrap utilities cheat sheet', exact: true }),
  ).toHaveCount(0);

  await page.getByRole('button', { name: 'Clear search' }).click();
  await expect(
    page.getByRole('link', { name: 'Angular signals deep dive', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Bootstrap utilities cheat sheet', exact: true }),
  ).toBeVisible();
});

test('no matches surfaces the empty state with the active query', async ({ page }) => {
  const user = makeUser({ email: 'searcher2@example.test', password: 'Search2Pass!' });
  const backend = createBackend({
    users: [user],
    currentUserId: user.id,
    links: [makeLink({ title: 'Angular signals deep dive', url: 'https://angular.dev' })],
  });
  await installApiMocks(page, backend);

  await page.goto('/app/links');
  await page
    .getByPlaceholder('Search by title, description, or URL')
    .fill('nothing-matches-this');

  await expect(page.getByText('No links match', { exact: false })).toBeVisible();
  await expect(page.getByText('nothing-matches-this')).toBeVisible();
});
