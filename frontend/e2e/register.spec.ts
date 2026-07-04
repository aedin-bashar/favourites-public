import { expect, test } from '@playwright/test';
import { createBackend, installApiMocks } from './helpers/fake-backend';

/**
 * Task 2.103 — end-to-end test for registration.
 *
 * Walks a brand-new visitor through the /register form: filling each field,
 * submitting, and landing on /app/dashboard once the backend confirms the
 * new account. The auth response body is what the AuthService stores in its
 * signal, so a successful POST is enough to satisfy the `authGuard` on /app.
 *
 * Form fields are addressed by their input `id` because the `*` required
 * marker after each `<label>` text node throws off accessible-name matching
 * for "Password" specifically — `getByLabel('Password', exact: true)` times
 * out in Chromium even though the asterisk span is `aria-hidden`.
 */
test('user can register and is taken to the dashboard', async ({ page }) => {
  const backend = createBackend();
  await installApiMocks(page, backend);

  await page.goto('/register');
  await expect(page).toHaveURL(/\/register$/);
  await expect(page.getByRole('heading', { name: 'Create your account' })).toBeVisible();

  await page.locator('#register-name').fill('Ada Lovelace');
  await page.locator('#register-email').fill('ada@example.test');
  await page.locator('#register-password').fill('CorrectHorseBattery9!');
  await page.locator('#register-confirm').fill('CorrectHorseBattery9!');

  const registerRequest = page.waitForRequest(
    (req) => req.url().endsWith('/api/auth/register') && req.method() === 'POST',
  );

  await page.getByRole('button', { name: 'Create account' }).click();

  const sent = await registerRequest;
  expect(sent.postDataJSON()).toMatchObject({
    displayName: 'Ada Lovelace',
    email: 'ada@example.test',
    password: 'CorrectHorseBattery9!',
    confirmPassword: 'CorrectHorseBattery9!',
  });

  await expect(page).toHaveURL(/\/app(\/dashboard)?$/);
  expect(backend.users).toHaveLength(1);
  expect(backend.users[0].email).toBe('ada@example.test');
  expect(backend.currentUserId).toBe(backend.users[0].id);
});

test('duplicate email surfaces the server error inline', async ({ page }) => {
  const backend = createBackend({
    users: [
      {
        id: 'existing-user',
        email: 'taken@example.test',
        displayName: 'Already Here',
        password: 'SomePassword1!',
      },
    ],
  });
  await installApiMocks(page, backend);

  await page.goto('/register');
  await page.locator('#register-name').fill('Second Try');
  await page.locator('#register-email').fill('taken@example.test');
  await page.locator('#register-password').fill('AnotherPass9!');
  await page.locator('#register-confirm').fill('AnotherPass9!');
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page.getByRole('alert')).toContainText(/already exists/i);
  await expect(page).toHaveURL(/\/register$/);
});
