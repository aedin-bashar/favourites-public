# Favourites

Favourites is a personal link manager for saving, organizing, searching, and revisiting important links.

## Product Summary

Users can create an account, save links with titles and descriptions, organize them with tags and categories, search and filter their collection, archive and restore links, import and export bookmarks, and customize the app from a responsive interface. The app also supports password recovery and account deletion.

Every link, tag, and category belongs to the authenticated user who created it, so users can manage only their own data.

## Tech Stack

| Area | Version |
|---|---|
| Backend SDK | .NET SDK 10.x |
| Backend target framework | `net10.0` |
| Backend language | C# 14.0 |
| Backend architecture | Layered monolith following Clean Architecture principles (Domain / Application / Infrastructure / API) |
| Auth | ASP.NET Core Identity with cookie authentication |
| Frontend framework | Angular 21.x (standalone components, strict TypeScript, SCSS) |
| Frontend styling | `bootstrap@5.3.8` |
| Frontend icons | `@fortawesome/fontawesome-free@7.x` |
| Database | SQL Server 2022 (EF Core) |

## Prerequisites

Before running the application locally, install:

- .NET SDK 10.x.
- A Node.js version supported by Angular 21.
- npm.
- Git.
- SQL Server 2022, either as a local instance or a local Docker container.

A SQL Server management tool, such as SQL Server Management Studio or `sqlcmd`, is optional but useful.

Verify the main tools with:

```bash
dotnet --version
dotnet --list-sdks
node --version
npm --version
```

## Local Startup

### Backend API

```bash
cd src/Favourites.Api
dotnet restore
dotnet run
```

By default, the API runs at `http://localhost:5069`. In Development, Swagger UI is available at `http://localhost:5069/swagger`.

### Frontend

```bash
cd frontend
npm install
npm start
```

The Angular 21 development server runs at `http://localhost:4200`. Requests to `/api` are proxied to `http://localhost:5069`; the API also allows the frontend origin through its Development-only CORS policy.

Run the API and the frontend in separate terminals; there is no combined launcher script.

### Database

Run SQL Server 2022 locally before starting features that need persistence. The API connects via the EF Core SQL Server provider using the connection string in `src/Favourites.Api/appsettings.Development.json`.

Before the first local registration, configure `ConnectionStrings:DefaultConnection` in `src/Favourites.Api/appsettings.Development.json`. You can replace the entire connection string or, if its other defaults suit your environment, replace `__REPLACE_WITH_LOCAL_SQL_PASSWORD__` with the password for your local `sa` account. Then apply the migrations from the repository root. Install the `dotnet-ef` tool first if necessary, as described under [EF Core Migrations](#ef-core-migrations).

```bash
dotnet ef database update --project src/Favourites.Infrastructure --startup-project src/Favourites.Api --context FavouritesDbContext
```

The committed development password is only a placeholder. Registration will fail if SQL Server is unavailable, the connection string is invalid, or migrations have not been applied because the required Identity tables will not be available.

Password-recovery emails also require valid values in the `Email` section of `src/Favourites.Api/appsettings.Development.json` and access to the configured SMTP server.

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
   - **Settings**: update the display name; manage theme, density, link defaults, sorting, and notification preferences; import or export bookmarks; delete archived links in bulk; delete the account.
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

Additional browser and viewport projects are opt-in through environment variables:

- `FAVOURITES_E2E_FIREFOX=1` enables Firefox and Microsoft Edge desktop projects.
- `FAVOURITES_E2E_WEBKIT=1` enables the desktop WebKit project.
- `FAVOURITES_E2E_MOBILE=1` enables Pixel 7, iPhone 14 Pro, and iPad Pro viewport projects.

Install any additional Playwright browser binaries before enabling their projects. Microsoft Edge must be installed separately because that project uses the local `msedge` browser channel.

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
