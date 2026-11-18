import { expect, test, type Page } from '@playwright/test';
import { mockApi } from './support/mock-api';

// Status tracking and cancel while Not Started (#31). Polling runs every 3 s, so waits are generous.

const startButton = (p: Page) => p.getByRole('button', { name: 'Start order', exact: true });
const POLL_WAIT = { timeout: 10_000 };

async function placeOrder(page: Page): Promise<void> {
  await page.goto('/');
  await startButton(page).click();
  await page.getByTestId('size-large').click();
  await page.getByTestId('add-to-order').click();
  await expect(page.getByTestId('total')).toBeVisible();
  await page.getByTestId('place-order').click();
  await expect(page).toHaveURL('/orders/order-1');
}

test('the status page polls and follows the kitchen', async ({ page }) => {
  const api = await mockApi(page);
  await placeOrder(page);

  await expect(page.getByTestId('status-text')).toContainText('not started yet');
  await expect(page.getByTestId('cancel-order')).toBeVisible();

  api.order!.status = 'Started';
  await expect(page.getByTestId('status-text')).toContainText('making your pizza', POLL_WAIT);
  await expect(page.getByTestId('step-Started')).toHaveAttribute('aria-current', 'step');
  await expect(page.getByTestId('cancel-order')).toHaveCount(0);

  api.order!.status = 'Delivered';
  await expect(page.getByTestId('status-text')).toContainText('Enjoy your pizza', POLL_WAIT);
});

test('cancelling while Not Started shows a cancelled confirmation', async ({ page }) => {
  const api = await mockApi(page);
  await placeOrder(page);

  await page.getByTestId('cancel-order').click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Keep my order', exact: true })
    .click();
  await expect(page.getByTestId('cancelled')).toHaveCount(0);

  await page.getByTestId('cancel-order').click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Yes, cancel order', exact: true })
    .click();

  await expect(page.getByTestId('cancelled')).toContainText('Your order has been cancelled.');
  await expect(page.getByTestId('cancel-order')).toHaveCount(0);
  expect(api.order).toBeNull();
});

test('if the kitchen starts first, cancel explains and refreshes the status', async ({ page }) => {
  const api = await mockApi(page);
  await placeOrder(page);

  api.order!.status = 'Started'; // the page has not polled yet, so it still offers Cancel
  await page.getByTestId('cancel-order').click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Yes, cancel order', exact: true })
    .click();

  await expect(page.getByTestId('status-notice')).toContainText('can no longer be cancelled');
  await expect(page.getByTestId('status-text')).toContainText('making your pizza');
  await expect(page.getByTestId('cancel-order')).toHaveCount(0);
  await expect(page.getByTestId('cancelled')).toHaveCount(0);
});
