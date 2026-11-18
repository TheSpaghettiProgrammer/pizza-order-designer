import { defineConfig, devices } from '@playwright/test';

// End-to-end tests that drive the real Quasar app in a browser (see docs/DECISIONS.md, D-034).
// Vitest (vitest.config.ts) stays for unit tests; Playwright only picks up test/e2e/.
//
// Playwright starts its own `quasar dev` on a dedicated port (default 9100), so a dev server you
// already have open on :9000 (possibly from another checkout) is never tested by mistake. The dev
// server still proxies /api to the API (API_PROXY_TARGET, default http://localhost:5158).
const port = Number(process.env.PLAYWRIGHT_PORT ?? 9100);
const baseURL = `http://localhost:${port}`;
const isCI = !!process.env.CI;

export default defineConfig({
  testDir: './test/e2e',
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  ...(isCI ? { workers: 1 } : {}),
  reporter: [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL,
    trace: 'on-first-retry',
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  webServer: {
    command: `npx quasar dev --port ${port}`,
    url: baseURL,
    reuseExistingServer: !isCI,
    timeout: 120_000,
  },
});
