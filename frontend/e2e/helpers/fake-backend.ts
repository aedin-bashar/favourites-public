import type { Page } from '@playwright/test';

/**
 * Tiny in-memory backend used by the Phase 2 e2e suite (Tasks 2.103-2.108).
 *
 * The Playwright `webServer` only starts the Angular dev server, so the real
 * ASP.NET Core API is not running during the suite. {@link installApiMocks}
 * intercepts same-origin `/api/*` requests and serves a canned response
 * shaped like the real API contract. State lives in a {@link BackendState}
 * record so each spec can pre-seed users / links and assert how the UI
 * mutated them.
 *
 * The Angular `AuthService` keeps the signed-in user in a signal that is
 * populated by the response body of /api/auth/login | /register | current-user.
 * That means the auth cookie itself doesn't need to flow correctly through
 * the browser — the mocked response body is what drives the front-end state.
 * Mutations to {@link BackendState.currentUserId} control whether
 * /api/auth/current-user returns 200 (guard passes) or 401 (guard bounces).
 */

export interface MockUser {
  id: string;
  email: string;
  displayName: string;
  password: string;
}

export interface MockTag {
  id: string;
  name: string;
}

export interface MockCategory {
  id: string;
  name: string;
}

export interface MockLink {
  id: string;
  url: string;
  title: string;
  description: string | null;
  isArchived: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  tags: MockTag[];
  category: MockCategory | null;
}

export interface BackendState {
  users: MockUser[];
  currentUserId: string | null;
  links: MockLink[];
  tags: MockTag[];
  categories: MockCategory[];
}

export function createBackend(seed: Partial<BackendState> = {}): BackendState {
  return {
    users: seed.users ?? [],
    currentUserId: seed.currentUserId ?? null,
    links: seed.links ?? [],
    tags: seed.tags ?? [],
    categories: seed.categories ?? [],
  };
}

let idCounter = 0;
function uuid(): string {
  // Stable-but-unique synthetic UUID. Real UUIDs would also be fine, but the
  // sequential counter keeps test failures readable.
  idCounter += 1;
  const seq = idCounter.toString(16).padStart(12, '0');
  return `00000000-0000-4000-8000-${seq}`;
}

export function makeUser(overrides: Partial<MockUser> = {}): MockUser {
  return {
    id: overrides.id ?? uuid(),
    email: overrides.email ?? `user-${idCounter}@example.test`,
    displayName: overrides.displayName ?? 'Test User',
    password: overrides.password ?? 'CorrectHorseBattery9!',
  };
}

export function makeLink(overrides: Partial<MockLink> = {}): MockLink {
  const created = overrides.createdAtUtc ?? new Date(2026, 4, 18, 12, 0, 0).toISOString();
  return {
    id: overrides.id ?? uuid(),
    url: overrides.url ?? 'https://example.com/article',
    title: overrides.title ?? 'Example article',
    description: overrides.description ?? null,
    isArchived: overrides.isArchived ?? false,
    createdAtUtc: created,
    updatedAtUtc: overrides.updatedAtUtc ?? null,
    tags: overrides.tags ?? [],
    category: overrides.category ?? null,
  };
}

interface CreateLinkBody {
  url?: string;
  title?: string;
  description?: string | null;
  tagIds?: string[] | null;
  categoryId?: string | null;
}

interface UpdateLinkBody extends CreateLinkBody {}

interface LoginBody {
  email?: string;
  password?: string;
}

interface RegisterBody {
  displayName?: string;
  email?: string;
  password?: string;
}

type LinksSortOrder = 'newest' | 'oldest' | 'title' | 'recently-updated' | 'oldest-archived';
type LinksArchivedFilter = 'active' | 'archived' | 'all';

function corsHeaders(): Record<string, string> {
  return {
    'access-control-allow-origin': 'http://localhost:4200',
    'access-control-allow-credentials': 'true',
    'access-control-allow-headers': 'content-type',
    'access-control-allow-methods': 'GET,POST,PUT,DELETE,OPTIONS',
  };
}

function jsonResponseHeaders(): Record<string, string> {
  return { 'content-type': 'application/json', ...corsHeaders() };
}

function userResponse(user: MockUser): { id: string; displayName: string; email: string } {
  return { id: user.id, displayName: user.displayName, email: user.email };
}

export async function installApiMocks(page: Page, backend: BackendState): Promise<void> {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const method = request.method().toUpperCase();
    const url = new URL(request.url());
    const path = url.pathname;

    if (method === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: corsHeaders() });
      return;
    }

    // ── Auth ────────────────────────────────────────────────────────────
    if (path === '/api/auth/register' && method === 'POST') {
      const body = (request.postDataJSON() ?? {}) as RegisterBody;
      const email = (body.email ?? '').trim();
      if (backend.users.some((u) => u.email.toLowerCase() === email.toLowerCase())) {
        await route.fulfill({
          status: 409,
          headers: jsonResponseHeaders(),
          body: JSON.stringify({ message: 'An account with this email already exists.' }),
        });
        return;
      }
      const user = makeUser({
        email,
        displayName: body.displayName ?? '',
        password: body.password ?? '',
      });
      backend.users.push(user);
      backend.currentUserId = user.id;
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify(userResponse(user)),
      });
      return;
    }

    if (path === '/api/auth/login' && method === 'POST') {
      const body = (request.postDataJSON() ?? {}) as LoginBody;
      const match = backend.users.find(
        (u) =>
          u.email.toLowerCase() === (body.email ?? '').toLowerCase() &&
          u.password === (body.password ?? ''),
      );
      if (!match) {
        await route.fulfill({
          status: 401,
          headers: jsonResponseHeaders(),
          body: JSON.stringify({ message: 'Invalid email or password.' }),
        });
        return;
      }
      backend.currentUserId = match.id;
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify(userResponse(match)),
      });
      return;
    }

    if (path === '/api/auth/logout' && method === 'POST') {
      backend.currentUserId = null;
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify({}),
      });
      return;
    }

    if (path === '/api/auth/current-user' && method === 'GET') {
      const user = backend.users.find((u) => u.id === backend.currentUserId);
      if (!user) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify(userResponse(user)),
      });
      return;
    }

    // ── Tags ────────────────────────────────────────────────────────────
    if (path === '/api/dashboard/summary' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify({
          totalLinks: backend.links.filter((link) => !link.isArchived).length,
          totalTags: backend.tags.length,
          totalCategories: backend.categories.length,
          totalArchived: backend.links.filter((link) => link.isArchived).length,
          thisWeek: {
            linksAdded: backend.links.length,
            categoriesCreated: backend.categories.length,
            tagsCreated: backend.tags.length,
            linksArchived: backend.links.filter((link) => link.isArchived).length,
          },
        }),
      });
      return;
    }

    if (path === '/api/tags/summary' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const tags = backend.tags.map((tag) => tagResponse(tag, backend));
      const sortedByUse = [...tags].sort((a, b) => b.linkCount - a.linkCount);
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify({
          totalTags: backend.tags.length,
          unusedTags: tags.filter((tag) => tag.linkCount === 0).length,
          mostUsed: sortedByUse[0]
            ? { id: sortedByUse[0].id, name: sortedByUse[0].name, count: sortedByUse[0].linkCount }
            : null,
          recentlyAdded: tags[0] ? { id: tags[0].id, name: tags[0].name } : null,
          possibleDuplicates: 0,
        }),
      });
      return;
    }

    if (path === '/api/tags' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const search = (url.searchParams.get('search') ?? '').trim().toLowerCase();
      const status = url.searchParams.get('status');
      const sort = url.searchParams.get('sort');
      let items = backend.tags.map((tag) => tagResponse(tag, backend));
      if (search) items = items.filter((tag) => tag.name.toLowerCase().includes(search));
      if (status === 'used') items = items.filter((tag) => tag.linkCount > 0);
      if (status === 'unused') items = items.filter((tag) => tag.linkCount === 0);
      items.sort(tagSorter(sort));
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify(pageEnvelope(items, url)),
      });
      return;
    }

    // ── Categories ──────────────────────────────────────────────────────
    if (path === '/api/categories/summary' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const categories = backend.categories.map((category) => categoryResponse(category, backend));
      const sortedBySize = [...categories].sort((a, b) => b.linkCount - a.linkCount);
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify({
          totalCategories: backend.categories.length,
          emptyCategories: categories.filter((category) => category.linkCount === 0).length,
          largestCategory: sortedBySize[0]
            ? {
                id: sortedBySize[0].id,
                name: sortedBySize[0].name,
                count: sortedBySize[0].linkCount,
              }
            : null,
          recentlyAdded: categories[0] ? { id: categories[0].id, name: categories[0].name } : null,
          uncategorizedLinks: backend.links.filter((link) => !link.category).length,
        }),
      });
      return;
    }

    if (path === '/api/categories' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const search = (url.searchParams.get('search') ?? '').trim().toLowerCase();
      const status = url.searchParams.get('status');
      const sort = url.searchParams.get('sort');
      let items = backend.categories.map((category) => categoryResponse(category, backend));
      if (search) items = items.filter((category) => category.name.toLowerCase().includes(search));
      if (status === 'used') items = items.filter((category) => category.linkCount > 0);
      if (status === 'empty') items = items.filter((category) => category.linkCount === 0);
      items.sort(categorySorter(sort));
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify(pageEnvelope(items, url)),
      });
      return;
    }

    // ── Links list / create ─────────────────────────────────────────────
    if (path === '/api/archived/summary' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const archived = backend.links
        .filter((link) => link.isArchived)
        .sort((a, b) => archivedTime(a).localeCompare(archivedTime(b)));
      const monthStart = new Date();
      monthStart.setUTCDate(1);
      monthStart.setUTCHours(0, 0, 0, 0);
      const cleanupCutoff = new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString();
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify({
          archivedLinks: archived.length,
          archivedThisMonth: archived.filter((link) => archivedTime(link) >= monthStart.toISOString()).length,
          oldestArchived: archived[0] ? linkResponse(archived[0], backend) : null,
          restoredRecently: backend.links.filter((link) => !link.isArchived && link.updatedAtUtc).length,
          cleanupSuggestions: archived
            .filter((link) => archivedTime(link) < cleanupCutoff)
            .slice(0, 5)
            .map((link) => linkResponse(link, backend)),
        }),
      });
      return;
    }

    if (path === '/api/links' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const search = (url.searchParams.get('search') ?? '').trim().toLowerCase();
      const tagId = url.searchParams.get('tagId');
      const categoryId = url.searchParams.get('categoryId');
      const sort = parseSort(url.searchParams.get('sort'));
      const archived = parseArchived(url.searchParams.get('archived'));
      const archivedFrom = url.searchParams.get('archivedFrom');
      const archivedTo = url.searchParams.get('archivedTo');
      let items = backend.links.slice();
      if (archived === 'active') {
        items = items.filter((l) => !l.isArchived);
      } else if (archived === 'archived') {
        items = items.filter((l) => l.isArchived);
      }
      if (search) {
        items = items.filter(
          (l) =>
            l.title.toLowerCase().includes(search) ||
            (l.description ?? '').toLowerCase().includes(search) ||
            l.url.toLowerCase().includes(search),
        );
      }
      if (tagId) items = items.filter((l) => l.tags.some((t) => t.id === tagId));
      if (categoryId) items = items.filter((l) => l.category?.id === categoryId);
      if (archivedFrom) {
        items = items.filter((l) => archivedTime(l) >= archivedFrom);
      }
      if (archivedTo) {
        items = items.filter((l) => archivedTime(l) <= archivedTo);
      }
      items.sort(linkSorter(sort));
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify(pageEnvelope(items.map((link) => linkResponse(link, backend)), url)),
      });
      return;
    }

    if (path === '/api/links' && method === 'POST') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const body = (request.postDataJSON() ?? {}) as CreateLinkBody;
      const tags = (body.tagIds ?? [])
        .map((id) => backend.tags.find((t) => t.id === id))
        .filter((t): t is MockTag => !!t);
      const category = body.categoryId
        ? backend.categories.find((c) => c.id === body.categoryId) ?? null
        : null;
      const link = makeLink({
        url: body.url ?? '',
        title: body.title ?? '',
        description: body.description ?? null,
        tags,
        category,
        createdAtUtc: new Date().toISOString(),
      });
      backend.links.push(link);
      await route.fulfill({
        status: 201,
        headers: { ...jsonResponseHeaders(), location: `/api/links/${link.id}` },
        body: JSON.stringify(linkResponse(link, backend)),
      });
      return;
    }

    // ── Links by id ─────────────────────────────────────────────────────
    const archiveMatch = path.match(/^\/api\/links\/([^/]+)\/(archive|restore)$/);
    if (archiveMatch && method === 'POST') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const [, id, action] = archiveMatch;
      const link = backend.links.find((l) => l.id === id);
      if (!link) {
        await route.fulfill({ status: 404, headers: corsHeaders(), body: '' });
        return;
      }

      link.isArchived = action === 'archive';
      link.updatedAtUtc = new Date().toISOString();
      await route.fulfill({ status: 204, headers: corsHeaders(), body: '' });
      return;
    }

    const linkIdMatch = path.match(/^\/api\/links\/([^/]+)$/);
    if (linkIdMatch) {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const id = linkIdMatch[1];
      const index = backend.links.findIndex((l) => l.id === id);
      if (index === -1) {
        await route.fulfill({ status: 404, headers: corsHeaders(), body: '' });
        return;
      }

      if (method === 'GET') {
        await route.fulfill({
          status: 200,
          headers: jsonResponseHeaders(),
          body: JSON.stringify(linkResponse(backend.links[index], backend)),
        });
        return;
      }

      if (method === 'PUT') {
        const body = (request.postDataJSON() ?? {}) as UpdateLinkBody;
        const previous = backend.links[index];
        const tags = (body.tagIds ?? [])
          .map((tagId) => backend.tags.find((t) => t.id === tagId))
          .filter((t): t is MockTag => !!t);
        const category = body.categoryId
          ? backend.categories.find((c) => c.id === body.categoryId) ?? null
          : null;
        const updated: MockLink = {
          ...previous,
          url: body.url ?? previous.url,
          title: body.title ?? previous.title,
          description: body.description ?? null,
          tags,
          category,
          updatedAtUtc: new Date().toISOString(),
        };
        backend.links[index] = updated;
        await route.fulfill({
          status: 200,
          headers: jsonResponseHeaders(),
          body: JSON.stringify(linkResponse(updated, backend)),
        });
        return;
      }

      if (method === 'DELETE') {
        backend.links.splice(index, 1);
        await route.fulfill({ status: 204, headers: corsHeaders(), body: '' });
        return;
      }
    }

    // ── User preferences / profile ──────────────────────────────────────
    // ── Tags duplicates / merge ─────────────────────────────────────────
    if (path === '/api/tags/duplicates' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({ status: 200, headers: jsonResponseHeaders(), body: JSON.stringify([]) });
      return;
    }

    if (path === '/api/tags/merge' && method === 'POST') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({ status: 200, headers: jsonResponseHeaders(), body: JSON.stringify({ merged: 0 }) });
      return;
    }

    // ── Categories duplicates / merge ───────────────────────────────────
    if (path === '/api/categories/duplicates' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({ status: 200, headers: jsonResponseHeaders(), body: JSON.stringify([]) });
      return;
    }

    if (path === '/api/categories/merge' && method === 'POST') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({ status: 200, headers: jsonResponseHeaders(), body: JSON.stringify({ merged: 0 }) });
      return;
    }

    // ── Links batch / cleanup ───────────────────────────────────────────
    if (path === '/api/links/restore-many' && method === 'POST') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const body = (request.postDataJSON() ?? {}) as { linkIds?: string[] };
      const ids = new Set(body.linkIds ?? []);
      let restored = 0;
      for (const link of backend.links) {
        if (ids.has(link.id) && link.isArchived) {
          link.isArchived = false;
          link.updatedAtUtc = new Date().toISOString();
          restored++;
        }
      }
      await route.fulfill({ status: 200, headers: jsonResponseHeaders(), body: JSON.stringify({ restored }) });
      return;
    }

    if (path === '/api/links/archived' && method === 'DELETE') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const before = backend.links.length;
      backend.links = backend.links.filter((l) => !l.isArchived);
      await route.fulfill({ status: 200, headers: jsonResponseHeaders(), body: JSON.stringify({ deleted: before - backend.links.length }) });
      return;
    }

    if (path === '/api/links/cleanup-suggestions' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const cutoff = new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString();
      const suggestions = backend.links
        .filter((l) => l.isArchived && archivedTime(l) < cutoff)
        .slice(0, 10)
        .map((l) => linkResponse(l, backend));
      await route.fulfill({ status: 200, headers: jsonResponseHeaders(), body: JSON.stringify(suggestions) });
      return;
    }

    if (path === '/api/links/export' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: { 'content-type': 'application/json', 'content-disposition': 'attachment; filename=favourites-export.json', ...corsHeaders() },
        body: JSON.stringify(backend.links.map((l) => linkResponse(l, backend))),
      });
      return;
    }

    if (path === '/api/links/import' && method === 'POST') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({ status: 200, headers: jsonResponseHeaders(), body: JSON.stringify({ created: 0, skipped: 0 }) });
      return;
    }

    // ── User preferences / profile ──────────────────────────────────────
    if (path === '/api/user/preferences' && method === 'GET') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify({
          theme: 'light',
          density: 'comfortable',
          defaultCategoryId: null,
          autoExtractTitle: true,
          showFavicon: true,
          openInNewTab: true,
          confirmBeforeDelete: true,
          suggestTagsAutomatically: true,
          showColorsOnTagChips: true,
          tagsDefaultSort: 'name',
          categoriesDefaultSort: 'name',
          weeklySummaryEmail: false,
          securityAlerts: true,
          productUpdates: false,
          updatedAtUtc: new Date().toISOString(),
        }),
      });
      return;
    }

    if (path === '/api/user/preferences' && method === 'PATCH') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const body = request.postDataJSON() ?? {};
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify({ ...body, updatedAtUtc: new Date().toISOString() }),
      });
      return;
    }

    if (path === '/api/user/profile' && method === 'PATCH') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const user = backend.users.find((u) => u.id === backend.currentUserId);
      if (!user) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      const body = (request.postDataJSON() ?? {}) as { displayName?: string };
      if (body.displayName) user.displayName = body.displayName;
      await route.fulfill({
        status: 200,
        headers: jsonResponseHeaders(),
        body: JSON.stringify(userResponse(user)),
      });
      return;
    }

    if (path === '/api/user/account' && method === 'DELETE') {
      if (!backend.currentUserId) {
        await route.fulfill({ status: 401, headers: corsHeaders(), body: '' });
        return;
      }
      backend.currentUserId = null;
      await route.fulfill({ status: 204, headers: corsHeaders(), body: '' });
      return;
    }

    // Unknown route — fail loudly so unhandled API calls surface in CI logs.
    await route.fulfill({
      status: 501,
      headers: jsonResponseHeaders(),
      body: JSON.stringify({ message: `Unhandled ${method} ${path}` }),
    });
  });
}

function parseSort(value: string | null): LinksSortOrder {
  if (
    value === 'oldest' ||
    value === 'title' ||
    value === 'recently-updated' ||
    value === 'oldest-archived'
  ) {
    return value;
  }
  return 'newest';
}

function parseArchived(value: string | null): LinksArchivedFilter {
  if (value === 'archived' || value === 'all') {
    return value;
  }
  return 'active';
}

function linkSorter(sort: LinksSortOrder): (a: MockLink, b: MockLink) => number {
  return (a, b) => {
    let result: number;
    if (sort === 'oldest') {
      result = a.createdAtUtc.localeCompare(b.createdAtUtc);
    } else if (sort === 'title') {
      result = a.title.localeCompare(b.title);
    } else if (sort === 'recently-updated') {
      result = archivedTime(b).localeCompare(archivedTime(a));
    } else if (sort === 'oldest-archived') {
      result = archivedTime(a).localeCompare(archivedTime(b));
    } else {
      result = b.createdAtUtc.localeCompare(a.createdAtUtc);
    }

    return result || a.id.localeCompare(b.id);
  };
}

function archivedTime(link: MockLink): string {
  return link.updatedAtUtc ?? link.createdAtUtc;
}

function pageEnvelope<T>(items: readonly T[], url: URL): {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
} {
  const page = Math.max(1, parseInt(url.searchParams.get('page') ?? '1', 10) || 1);
  const pageSize = Math.max(1, parseInt(url.searchParams.get('pageSize') ?? '25', 10) || 25);
  const start = (page - 1) * pageSize;
  return {
    items: items.slice(start, start + pageSize),
    total: items.length,
    page,
    pageSize,
  };
}

function tagResponse(tag: MockTag, backend: BackendState): MockTag & {
  linkCount: number;
  createdAtUtc: string;
  lastUsedAtUtc: string | null;
} {
  const links = backend.links.filter((link) => link.tags.some((item) => item.id === tag.id));
  const lastUsedAtUtc = links
    .map(archivedTime)
    .sort((a, b) => b.localeCompare(a))[0] ?? null;
  return {
    ...tag,
    linkCount: links.length,
    createdAtUtc: new Date(2026, 4, 1, 12, 0, 0).toISOString(),
    lastUsedAtUtc,
  };
}

function categoryResponse(category: MockCategory, backend: BackendState): MockCategory & {
  linkCount: number;
  createdAtUtc: string;
  lastActivityAtUtc: string | null;
} {
  const links = backend.links.filter((link) => link.category?.id === category.id);
  const lastActivityAtUtc = links
    .map(archivedTime)
    .sort((a, b) => b.localeCompare(a))[0] ?? null;
  return {
    ...category,
    linkCount: links.length,
    createdAtUtc: new Date(2026, 4, 1, 12, 0, 0).toISOString(),
    lastActivityAtUtc,
  };
}

function linkResponse(link: MockLink, backend: BackendState): MockLink {
  return {
    ...link,
    tags: link.tags.map((tag) => tagResponse(tag, backend)),
    category: link.category ? categoryResponse(link.category, backend) : null,
  };
}

function tagSorter(sort: string | null): (a: ReturnType<typeof tagResponse>, b: ReturnType<typeof tagResponse>) => number {
  return (a, b) => {
    if (sort === 'most-used') return b.linkCount - a.linkCount || a.name.localeCompare(b.name);
    if (sort === 'least-used') return a.linkCount - b.linkCount || a.name.localeCompare(b.name);
    if (sort === 'newest') return b.createdAtUtc.localeCompare(a.createdAtUtc) || a.name.localeCompare(b.name);
    return a.name.localeCompare(b.name);
  };
}

function categorySorter(
  sort: string | null,
): (a: ReturnType<typeof categoryResponse>, b: ReturnType<typeof categoryResponse>) => number {
  return (a, b) => {
    if (sort === 'largest') return b.linkCount - a.linkCount || a.name.localeCompare(b.name);
    if (sort === 'newest') return b.createdAtUtc.localeCompare(a.createdAtUtc) || a.name.localeCompare(b.name);
    if (sort === 'recently-active' || sort === 'activity') {
      const aActivity = a.lastActivityAtUtc ?? a.createdAtUtc;
      const bActivity = b.lastActivityAtUtc ?? b.createdAtUtc;
      return bActivity.localeCompare(aActivity) || a.name.localeCompare(b.name);
    }
    return a.name.localeCompare(b.name);
  };
}
