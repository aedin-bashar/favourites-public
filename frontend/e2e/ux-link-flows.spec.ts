import { expect, test } from '@playwright/test';
import {
  createBackend,
  installApiMocks,
  makeLink,
  makeUser,
  type MockCategory,
  type MockTag,
} from './helpers/fake-backend';

test.describe('mobile login and create-link flow', () => {
  test.use({
    viewport: { width: 390, height: 844 },
    isMobile: true,
    hasTouch: true,
  });

  test('a mobile user can sign in, use the Add action, and save a pasted URL', async ({
    page,
  }) => {
    const user = makeUser({
      email: 'mobile.creator@example.test',
      password: 'MobileCreate9!',
      displayName: 'Mobile Creator',
    });
    const backend = createBackend({ users: [user] });
    await installApiMocks(page, backend);

    await page.goto('/login');
    await page.locator('#login-email').fill(user.email);
    await page.locator('#login-password').fill(user.password);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page).toHaveURL(/\/app(\/dashboard)?$/);
    await page.getByRole('button', { name: 'Add', exact: true }).click();

    await expect(page).toHaveURL(/\/app\/links\?add=1$/);
    await expect(page.getByRole('dialog', { name: 'Add link' })).toBeVisible();

    const url = 'https://example.com/mobile-create-link';
    const expectedTitle = 'Mobile create link';
    await page.locator('#add-link-url').fill(url);
    await expect(page.locator('#add-link-title')).toHaveValue(expectedTitle);

    const createRequest = page.waitForRequest(
      (req) => req.method() === 'POST' && req.url().endsWith('/api/links'),
    );
    await page.getByRole('button', { name: 'Save link' }).click();
    const request = await createRequest;

    expect(request.postDataJSON()).toMatchObject({
      url,
      title: expectedTitle,
      description: null,
      tagIds: [],
      categoryId: null,
    });
    expect(backend.links).toHaveLength(1);
    expect(backend.links[0].title).toBe(expectedTitle);
    await expect(page.getByRole('dialog', { name: 'Add link' })).toBeHidden();
    await expect(page.getByRole('link', { name: expectedTitle, exact: true })).toBeVisible();
  });
});

test('filtering and sorting links sends the selected query and reorders the list', async ({
  page,
}) => {
  const user = makeUser({ email: 'filter-sort@example.test', password: 'FilterSort9!' });
  const angularTag: MockTag = { id: 'tag-angular', name: 'Angular' };
  const dotnetTag: MockTag = { id: 'tag-dotnet', name: '.NET' };
  const learningCategory: MockCategory = { id: 'category-learning', name: 'Learning' };
  const backend = createBackend({
    users: [user],
    currentUserId: user.id,
    tags: [angularTag, dotnetTag],
    categories: [learningCategory],
    links: [
      makeLink({
        title: 'Zebra Angular guide',
        url: 'https://example.com/zebra-angular',
        tags: [angularTag],
        category: learningCategory,
        createdAtUtc: new Date(2026, 4, 18, 10, 0, 0).toISOString(),
      }),
      makeLink({
        title: 'Alpha Angular patterns',
        url: 'https://example.com/alpha-angular',
        tags: [angularTag],
        category: learningCategory,
        createdAtUtc: new Date(2026, 4, 17, 10, 0, 0).toISOString(),
      }),
      makeLink({
        title: 'Middle .NET notes',
        url: 'https://example.com/dotnet',
        tags: [dotnetTag],
        category: learningCategory,
        createdAtUtc: new Date(2026, 4, 19, 10, 0, 0).toISOString(),
      }),
    ],
  });
  await installApiMocks(page, backend);

  await page.goto('/app/links');
  await expect(page.getByRole('link', { name: 'Middle .NET notes', exact: true })).toBeVisible();

  const filterRequest = page.waitForRequest(
    (req) => req.method() === 'GET' && req.url().includes('/api/links?'),
  );
  await page.locator('#link-list-tag-filter').selectOption({ label: 'Angular' });
  const filtered = await filterRequest;
  expect(new URL(filtered.url()).searchParams.get('tagId')).toBe(angularTag.id);

  await expect(page.getByRole('link', { name: 'Zebra Angular guide', exact: true })).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Alpha Angular patterns', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Middle .NET notes', exact: true })).toHaveCount(0);

  const sortRequest = page.waitForRequest(
    (req) => req.method() === 'GET' && req.url().includes('/api/links?'),
  );
  await page.locator('#link-list-sort').selectOption('title');
  const sorted = await sortRequest;
  const sortedParams = new URL(sorted.url()).searchParams;
  expect(sortedParams.get('tagId')).toBe(angularTag.id);
  expect(sortedParams.get('sort')).toBe('title');

  await expect
    .poll(async () =>
      (await page.locator('.link-card__title').allTextContents()).map((title) => title.trim()),
    )
    .toEqual(['Alpha Angular patterns', 'Zebra Angular guide']);
});

test('a user can archive a link, view archived links, then restore it', async ({ page }) => {
  const user = makeUser({ email: 'archiver@example.test', password: 'Archive9!' });
  const link = makeLink({
    title: 'Archive candidate',
    url: 'https://example.com/archive-candidate',
    createdAtUtc: new Date(2026, 4, 18, 10, 0, 0).toISOString(),
  });
  const backend = createBackend({
    users: [user],
    currentUserId: user.id,
    links: [link],
  });
  await installApiMocks(page, backend);

  await page.goto('/app/links');
  await expect(page.getByRole('link', { name: 'Archive candidate', exact: true })).toBeVisible();

  const archiveRequest = page.waitForRequest(
    (req) => req.method() === 'POST' && req.url().endsWith(`/api/links/${link.id}/archive`),
  );
  await page.getByRole('button', { name: 'Archive Archive candidate' }).click();
  await archiveRequest;

  expect(link.isArchived).toBe(true);
  await expect(page.getByRole('link', { name: 'Archive candidate', exact: true })).toHaveCount(0);

  const archivedListRequest = page.waitForRequest(
    (req) => req.method() === 'GET' && req.url().includes('archived=archived'),
  );
  await page.getByRole('tab', { name: 'Archived' }).click();
  await archivedListRequest;
  await expect(page.getByRole('heading', { name: 'Archived Links' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Archive candidate', exact: true })).toBeVisible();
  await expect(page.locator('.link-card__archived', { hasText: 'Archived' })).toBeVisible();

  const restoreRequest = page.waitForRequest(
    (req) => req.method() === 'POST' && req.url().endsWith(`/api/links/${link.id}/restore`),
  );
  await page.getByRole('button', { name: 'Restore Archive candidate' }).click();
  await restoreRequest;

  expect(link.isArchived).toBe(false);
  await expect(page.getByText('No archived links')).toBeVisible();

  await page.getByRole('tab', { name: 'Active' }).click();
  await expect(page.getByRole('heading', { name: 'All Links' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Archive candidate', exact: true })).toBeVisible();
});
