import { defineConfig, devices } from '@playwright/test';

const skipWebServer = process.env['FAVOURITES_SKIP_PLAYWRIGHT_WEBSERVER'] === '1';

// R11.4 — opt-in flags for cross-browser and mobile viewport projects.
// Set FAVOURITES_E2E_FIREFOX=1, FAVOURITES_E2E_WEBKIT=1, or
// FAVOURITES_E2E_MOBILE=1 to enable the corresponding project(s).
const runFirefox = process.env['FAVOURITES_E2E_FIREFOX'] === '1';
const runWebKit = process.env['FAVOURITES_E2E_WEBKIT'] === '1';
const runMobile = process.env['FAVOURITES_E2E_MOBILE'] === '1';

/**
 * Playwright configuration for Favourites end-to-end tests.
 * ADR 0003 (docs/decisions/0003-use-playwright-for-e2e.md) — Chromium is the
 * required browser; Firefox and WebKit are opt-in per environment.
 *
 * Desktop viewports covered: Chrome, Firefox (opt-in), Safari/WebKit (opt-in),
 * Microsoft Edge (opt-in via chromium channel).
 * Mobile viewports covered: iPhone 14 Pro and Pixel 7 (opt-in).
 * Tablet viewport: iPad Pro (opt-in, part of mobile flag).
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  reporter: 'list',

  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },

  projects: [
    // ── Required ────────────────────────────────────────────────────────────
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },

    // ── Opt-in: cross-browser desktop ───────────────────────────────────────
    ...(runFirefox
      ? [{ name: 'firefox', use: { ...devices['Desktop Firefox'] } }]
      : []),
    ...(runWebKit
      ? [{ name: 'webkit', use: { ...devices['Desktop Safari'] } }]
      : []),
    // Edge uses the Chromium engine; run via channel flag.
    ...(runFirefox
      ? [{ name: 'edge', use: { ...devices['Desktop Chrome'], channel: 'msedge' } }]
      : []),

    // ── Opt-in: mobile & tablet viewports ───────────────────────────────────
    ...(runMobile
      ? [
          { name: 'mobile-chrome', use: { ...devices['Pixel 7'] } },
          { name: 'mobile-safari', use: { ...devices['iPhone 14 Pro'] } },
          { name: 'tablet-safari', use: { ...devices['iPad Pro 11'] } },
        ]
      : []),
  ],

  webServer: skipWebServer
    ? undefined
    : {
        command: 'node ./node_modules/@angular/cli/bin/ng.js serve',
        url: 'http://localhost:4200',
        reuseExistingServer: !process.env['CI'],
        timeout: 120_000,
      },
});
