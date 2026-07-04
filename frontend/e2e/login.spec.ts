import { expect, test } from '@playwright/test';
import { createBackend, installApiMocks, makeUser } from './helpers/fake-backend';

/**
 * Task 2.104 — end-to-end test for login.
 *
 * Seeds a known user, drives the /login form, and verifies the page lands
 * on /app once the AuthService stores the user from the login response.
 *
 * Fields are addressed by id (`#login-email`, `#login-password`) to avoid
 * the same accessible-name resolution issue documented in register.spec.ts.
 */
test('existing user can sign in and reach the authenticated app', async ({ page }) => {
  const user = makeUser({
    email: 'grace@example.test',
    password: 'TopSecret9!',
    displayName: 'Grace Hopper',
  });
  const backend = createBackend({ users: [user] });
  await installApiMocks(page, backend);

  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible();

  await page.locator('#login-email').fill(user.email);
  await page.locator('#login-password').fill(user.password);

  const loginRequest = page.waitForRequest(
    (req) => req.url().endsWith('/api/auth/login') && req.method() === 'POST',
  );

  await page.getByRole('button', { name: 'Sign in' }).click();

  const sent = await loginRequest;
  expect(sent.postDataJSON()).toMatchObject({
    email: user.email,
    password: user.password,
  });

  await expect(page).toHaveURL(/\/app(\/dashboard)?$/);
  expect(backend.currentUserId).toBe(user.id);
});

test('invalid credentials surface a server-side error', async ({ page }) => {
  const backend = createBackend({
    users: [makeUser({ email: 'someone@example.test', password: 'GoodPass9!' })],
  });
  await installApiMocks(page, backend);

  await page.goto('/login');
  await page.locator('#login-email').fill('someone@example.test');
  await page.locator('#login-password').fill('WrongPassword');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
  expect(backend.currentUserId).toBeNull();
});
