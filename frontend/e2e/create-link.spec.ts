import { expect, test } from '@playwright/test';
import { createBackend, installApiMocks, makeUser } from './helpers/fake-backend';

/**
 * Task 2.105 - end-to-end test for creating a link.
 * Drives the mounted All Links add-link modal and asserts the POST body plus
 * the refreshed list card.
 */
test('a signed-in user can create a link and see it on the All Links page', async ({ page }) => {
  const user = makeUser({ email: 'creator@example.test', password: 'CreateLink9!' });
  const backend = createBackend({ users: [user], currentUserId: user.id });
  await installApiMocks(page, backend);

  await page.goto('/app/links');
  await expect(page.getByRole('heading', { name: 'All Links' })).toBeVisible();
  await expect(page.getByText('No links to show yet')).toBeVisible();

  const createPayload = {
    url: 'https://playwright.dev/docs/api/class-page',
    title: 'Playwright Page API',
    description: 'Reference for the Page object.',
    tagIds: [],
    categoryId: null,
  };

  await page.getByRole('button', { name: 'Add link', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Add link' })).toBeVisible();

  await page.locator('#add-link-url').fill(createPayload.url);
  await page.locator('#add-link-title').fill(createPayload.title);
  await page.locator('#add-link-description').fill(createPayload.description);

  const createRequest = page.waitForRequest(
    (req) => req.method() === 'POST' && req.url().endsWith('/api/links'),
  );
  await page.getByRole('button', { name: 'Save link' }).click();
  const request = await createRequest;

  expect(request.postDataJSON()).toMatchObject(createPayload);
  expect(backend.links).toHaveLength(1);
  expect(backend.links[0]).toMatchObject({
    url: createPayload.url,
    title: createPayload.title,
    description: createPayload.description,
    isArchived: false,
  });

  await expect(page.getByRole('dialog', { name: 'Add link' })).toBeHidden();
  await expect(
    page.getByRole('link', { name: createPayload.title, exact: true }),
  ).toBeVisible();
});
