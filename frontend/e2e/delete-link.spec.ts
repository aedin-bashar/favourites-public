import { expect, test } from '@playwright/test';
import { createBackend, installApiMocks, makeLink, makeUser } from './helpers/fake-backend';

/**
 * Task 2.108 — end-to-end test for deleting a link.
 *
 * Drives the All Links page's per-card delete confirmation modal: click
 * delete on the card, accept the confirmation, and assert both the DELETE
 * call and that the card no longer renders. Title links use `exact: true`
 * because each card also exposes a sibling icon-only "Open <title>" anchor.
 */
test('user can delete a link from the All Links page via the confirm modal', async ({ page }) => {
  const user = makeUser({ email: 'deleter@example.test', password: 'DeletePass9!' });
  const keeper = makeLink({
    title: 'Keep this one',
    url: 'https://example.com/keep',
    createdAtUtc: new Date(2026, 4, 18, 10, 0, 0).toISOString(),
  });
  const target = makeLink({
    title: 'Delete this one',
    url: 'https://example.com/delete-me',
    createdAtUtc: new Date(2026, 4, 18, 9, 0, 0).toISOString(),
  });
  const backend = createBackend({
    users: [user],
    currentUserId: user.id,
    links: [keeper, target],
  });
  await installApiMocks(page, backend);

  await page.goto('/app/links');
  await expect(page.getByRole('link', { name: 'Keep this one', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Delete this one', exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Delete Delete this one' }).click();

  const dialog = page.getByRole('dialog', { name: 'Delete this link?' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText('Delete this one')).toBeVisible();

  const deleteRequest = page.waitForRequest(
    (req) => req.method() === 'DELETE' && req.url().endsWith(`/api/links/${target.id}`),
  );

  await dialog.getByRole('button', { name: 'Delete link' }).click();

  await deleteRequest;
  await expect(dialog).not.toBeVisible();
  await expect(page.getByRole('link', { name: 'Delete this one', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Keep this one', exact: true })).toBeVisible();
  expect(backend.links).toHaveLength(1);
  expect(backend.links[0].id).toBe(keeper.id);
});
