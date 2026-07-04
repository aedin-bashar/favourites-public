# Favourites

Favourites is a personal link manager for saving, organizing, searching, and opening important links from anywhere.

## Product Summary

Favourites is a fully redesigned link manager. A user can register an account, log in, save favourite links with a title and description, organize links with tags and categories, search and filter links, archive and restore links, import and export bookmarks, and manage settings — all from a responsive UI.

Every link, tag, and category belongs to the authenticated user who created it, so users can manage only their own data.

## Tech Stack

| Area | Version |
|---|---|
| Backend SDK | .NET SDK 10.x |
| Backend target framework | `net10.0` |
| Backend language | C# 14.0 |
| Backend architecture | Modular monolith, Clean Architecture (Domain / Application / Infrastructure / Api) |
| Auth | ASP.NET Core Identity with secure cookie authentication |
| Frontend framework | Angular 21.x (standalone components, strict TypeScript, SCSS) |
| Frontend styling | `bootstrap@5.3.8` |
| Frontend icons | `@fortawesome/fontawesome-free` |
| Database | SQL Server 2022 (EF Core) |

## Prerequisites

Before running the application locally, install:

- .NET SDK 10.x.
- Node.js compatible with Angular 21.
- npm.
- Angular CLI 21.x.
- Git.
- SQL Server 2022, either as a local instance or a local Docker container.
- A SQL Server management tool, such as SQL Server Management Studio, Azure Data Studio, or `sqlcmd`.

Verify the main tools with:

```bash
dotnet --version
dotnet --list-sdks
node --version
npm --version
ng version
```

## Local Startup

### Backend API

```bash
cd src/Favourites.Api
dotnet restore
dotnet run
```

The API runs as an ASP.NET Core Web API targeting `net10.0`. In Development, Swagger is available at `/swagger`.

### Frontend

```bash
cd frontend
npm install
npm start
```

The Angular 21 dev server runs at `http://localhost:4200` and is allowed by the API's local CORS policy (Development only).

Run the API and the frontend in separate terminals; there is no combined launcher script.

### Database

Run SQL Server 2022 locally before starting features that need persistence. The API connects via the EF Core SQL Server provider using the connection string in `src/Favourites.Api/appsettings.Development.json`.

Before the first local registration, replace the placeholder in `src/Favourites.Api/appsettings.Development.json` with your local SQL Server connection string and apply the migrations:

```bash
dotnet ef database update --project src/Favourites.Infrastructure --startup-project src/Favourites.Api --context FavouritesDbContext
```

For a default local SQL Server instance that is not listening on TCP port 1433, use `Server=localhost` instead of `Server=localhost,1433`.

The committed development connection string is only a placeholder. If SQL Server is not running, the password is still the placeholder, or migrations have not been applied, registration cannot create the Identity user tables it needs.

## Using the App Locally

1. Start the API and frontend in separate terminals.
2. Open `http://localhost:4200`.
3. Register a new account or sign in with an existing one.
4. Use the authenticated area under `/app`:
   - **Dashboard**: stat cards (total links, tags, categories, archived), quick-save URL form, recently added links, common tags, and this-week activity summary.
   - **All Links**: create links via the Add link modal or import bookmarks from a Netscape HTML file or a Favourites JSON export; search by title/description/URL; filter by tag, category, status; sort by newest, oldest, title, or recently updated; open, edit, archive/restore, and delete links; paginate results; export links to JSON or Netscape HTML.
   - **Tags**: create, rename, and delete tags; filter and sort the tag list; merge duplicate tags; view per-tag link counts and health metrics.
   - **Categories**: create, rename, and delete categories; filter and sort the category list; merge duplicate categories; view per-category link counts.
   - **Archived**: browse archived links with tag/category/date filters; restore individual links or a selection; empty the archive; review cleanup suggestions (links archived more than 90 days).
   - **Settings**: update profile (display name); manage preferences (theme, density, defaults, notifications); import/export bookmarks; delete archived links in bulk; delete account.
5. Link creation is available from the dashboard quick-save form, the All Links `Add link` modal, and through `POST /api/links`.

Quick save is optimized for paste-and-save: enter an `http` or `https` URL on the dashboard and press Enter or Save. The app derives an initial title from the URL.

On All Links, search, tag filtering, category filtering, sorting, and the Active/Archived/All status tabs can be combined. By default the list shows active links only. Archiving hides a link without deleting it; the Archived tab shows archived links with a Restore action.

All data is scoped to the signed-in user. The frontend does not send `UserId`; the API resolves ownership from the authentication cookie.

## Running Tests

### Backend (.NET)

```bash
dotnet test                                             # all backend tests
dotnet test tests/Favourites.UnitTests                  # domain/unit tests only
dotnet test tests/Favourites.IntegrationTests           # API/integration tests only
dotnet test --filter "FullyQualifiedName~LoginEndpointTests"   # single class
```

Integration tests use `WebApplicationFactory` with EF Core InMemory — no SQL Server required to run them.

### Frontend (Vitest via Angular 21)

```bash
cd frontend
npm test                  # run once (non-watching, CI-friendly)
npm run test:watch        # watch mode
```

### End-to-end (Playwright)

End-to-end testing uses Playwright. Specs live in `frontend/e2e/`.

```bash
cd frontend
npm run e2e:install       # one-time per machine — installs Chromium
npm run e2e               # run the suite headlessly
npm run e2e:ui            # open the Playwright UI runner
```

The `npm run e2e` command starts the Angular dev server if one is not already running at `http://localhost:4200`, runs Playwright, and then stops the server it started.

The current end-to-end suite covers: registration; login; creating a link via the All Links modal and mobile Add action; searching, filtering, and sorting links; editing links; archiving/restoring links; deleting links; and happy-path smoke tests for the Dashboard, Tags, Categories, Archived, and Settings pages (both desktop and mobile viewports). Specs install an in-memory fake backend with `page.route`, so `npm run e2e` only needs the Angular dev server.

Cross-browser and mobile viewport projects (Firefox, WebKit/Safari, Edge, Pixel 7, iPhone 14 Pro, iPad Pro) are opt-in via environment variables — see `frontend/playwright.config.ts` for the flag names (`FAVOURITES_E2E_FIREFOX`, `FAVOURITES_E2E_WEBKIT`, `FAVOURITES_E2E_MOBILE`).

### EF Core Migrations

Run EF Core migration commands from the repository root.

If `dotnet ef` is not installed yet, install it once:

```bash
dotnet tool install --global dotnet-ef
```

Create a new migration:

```bash
dotnet ef migrations add <MigrationName> --project src/Favourites.Infrastructure --startup-project src/Favourites.Api --context FavouritesDbContext --output-dir Persistence/Migrations
```

Apply the latest migrations to the configured local database:

```bash
dotnet ef database update --project src/Favourites.Infrastructure --startup-project src/Favourites.Api --context FavouritesDbContext
```

Remove the last migration before it has been applied to a shared database:

```bash
dotnet ef migrations remove --project src/Favourites.Infrastructure --startup-project src/Favourites.Api --context FavouritesDbContext
```

## Local Secrets Policy

Do not commit real secrets to the repository.

Local-only values include:

- SQL Server connection strings with usernames, passwords, hostnames, or database names that are private to a developer machine.
- Authentication secrets, cookie signing keys, data protection keys, and any future token or encryption secrets.
- Future third-party API keys for metadata fetching, email, analytics, AI features, or other integrations.

For local backend development, keep the database value under `ConnectionStrings:DefaultConnection` in the API appsettings file. Do not commit real production credentials.

For local frontend development, do not place private API keys in Angular environment files. Browser-delivered configuration is visible to users, so private integrations should go through the backend API.

Production secrets must be configured on the server or deployment platform, not committed to Git.

## License

MIT — see [LICENSE](LICENSE).
