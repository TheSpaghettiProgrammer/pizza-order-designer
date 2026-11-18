import { expect, test } from '@playwright/test';

// Smoke checks for the app shell. They prove the Playwright harness, the Quasar dev server and
// history-mode routing work together; feature specs start with #25.

test('home page shows the app shell', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('banner')).toContainText('Pizza Order Designer');
});

test('unknown deep link shows the 404 page and Go Home returns to the start', async ({ page }) => {
  await page.goto('/no/such/page');

  await expect(page.getByText('Oops. Nothing here...')).toBeVisible();

  await page.getByRole('link', { name: 'Go Home' }).click();

  await expect(page).toHaveURL('/');
  await expect(page.getByRole('banner')).toContainText('Pizza Order Designer');
});
