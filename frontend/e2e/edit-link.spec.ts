import { expect, test } from '@playwright/test';
import { createBackend, installApiMocks, makeLink, makeUser } from './helpers/fake-backend';

/**
 * Task 2.107 — end-to-end test for editing a link.
 *
 * Drives the only mounted edit surface: the link details page at
 * /app/links/:id. Opens the link, switches to edit mode, changes the title
 * and description, submits, and asserts both the PUT contract and the
 * refreshed read-only view.
 *
 * Edit-form fields are addressed by id (`#edit-link-url`, etc.) to avoid
 * label-name ambiguities described in register.spec.ts.
 */
test('user can edit an existing link from the details page', async ({ page }) => {
  const user = makeUser({ email: 'editor@example.test', password: 'EditPass9!' });
  const link = makeLink({
    title: 'Original title',
    url: 'https://example.com/original',
    description: 'Original description.',
  });
  const backend = createBackend({
    users: [user],
    currentUserId: user.id,
    links: [link],
  });
  await installApiMocks(page, backend);

  await page.goto(`/app/links/${link.id}`);
  await expect(page.getByRole('heading', { name: 'Original title' })).toBeVisible();

  await page.getByRole('button', { name: /^Edit/ }).click();
  await expect(page.getByRole('heading', { name: 'Edit link' })).toBeVisible();

  await page.locator('#edit-link-url').fill('https://example.com/updated');
  await page.locator('#edit-link-title').fill('Updated title');
  await page.locator('#edit-link-description').fill('Updated description.');

  const updateRequest = page.waitForRequest(
    (req) => req.method() === 'PUT' && req.url().endsWith(`/api/links/${link.id}`),
  );

  await page.getByRole('button', { name: 'Save changes' }).click();

  const sent = await updateRequest;
  expect(sent.postDataJSON()).toMatchObject({
    url: 'https://example.com/updated',
    title: 'Updated title',
    description: 'Updated description.',
  });

  await expect(page.getByRole('heading', { name: 'Updated title' })).toBeVisible();
  await expect(page.getByText('Updated description.')).toBeVisible();
  expect(backend.links[0]).toMatchObject({
    url: 'https://example.com/updated',
    title: 'Updated title',
    description: 'Updated description.',
  });
  expect(backend.links[0].updatedAtUtc).not.toBeNull();
});
