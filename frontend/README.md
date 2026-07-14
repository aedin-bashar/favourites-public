# Favourites Frontend

This directory contains the Angular 21 frontend for Favourites. It uses standalone components, strict TypeScript, SCSS, Bootstrap, and Font Awesome.

For full application setup, including the .NET API, SQL Server, and EF Core migrations, see the [repository README](../README.md).

## Prerequisites

- A Node.js version supported by Angular 21.
- npm.

The Angular CLI is installed locally with the project dependencies; a global installation is not required.

## Development

From this directory, install the dependencies and start the development server:

```bash
npm install
npm start
```

Open `http://localhost:4200`. The development server reloads the application when source files change.

Frontend requests use relative `/api` URLs. During local development, `proxy.conf.json` forwards them to the API at `http://localhost:5069`, so start the backend separately when using the application normally.

## Available Commands

| Command | Purpose |
|---|---|
| `npm start` | Start the Angular development server. |
| `npm run build` | Create an optimized production build in `dist/`. |
| `npm run watch` | Rebuild in development mode when source files change. |
| `npm test` | Run the Vitest unit tests once. |
| `npm run test:watch` | Run unit tests in watch mode. |
| `npm run e2e:install` | Install Chromium for Playwright. |
| `npm run e2e` | Run Playwright end-to-end tests headlessly. |
| `npm run e2e:ui` | Open the Playwright UI runner. |

## Unit Tests

Run the Angular unit tests with Vitest:

```bash
npm test
```

Use `npm run test:watch` while developing.

## End-to-End Tests

Install Chromium once, then run the Playwright suite:

```bash
npm run e2e:install
npm run e2e
```

The end-to-end runner starts the Angular development server if necessary and stops the server it started when the test run finishes. The specs mock `/api` requests in the browser, so the .NET API and SQL Server are not required for this suite.

Firefox, WebKit, Microsoft Edge, and mobile viewport projects are optional. See [playwright.config.ts](playwright.config.ts) and the root README's [end-to-end testing section](../README.md#end-to-end-playwright) for their environment flags and browser requirements.

## Code Generation

After installing dependencies, run the local Angular CLI through the project's `ng` npm script. For example:

```bash
npm run ng -- generate component features/example
```

Use `npm run ng -- generate --help` to list the available schematics and options.
