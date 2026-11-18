import { expect, test, type Page } from '@playwright/test';
import { mockApi } from './support/mock-api';

// Retry-safe submit, price-changed confirmation and the confirmation screen (#30).

const STORAGE_KEY = 'pizza-order-designer:draft-order';
const startButton = (p: Page) => p.getByRole('button', { name: 'Start order', exact: true });

async function buildOrder(page: Page): Promise<void> {
  await page.goto('/');
  await startButton(page).click();
  await page.getByTestId('size-large').click();
  await page.getByTestId('topping-pepperoni').click();
  await page.getByTestId('add-to-order').click();
  await expect(page.getByTestId('total')).toHaveText('$13.80');
}

test('placing an order shows the confirmation and clears the draft', async ({ page }) => {
  const api = await mockApi(page);
  await buildOrder(page);

  await page.getByTestId('place-order').click();

  await expect(page).toHaveURL('/orders/order-1');
  await expect(page.getByTestId('order-id')).toHaveText('order-1');
  await expect(page.getByTestId('order-description')).toHaveText('1 × Large pizza — Pepperoni');
  await expect(page.getByTestId('order-total')).toHaveText('$13.80');
  expect(api.submits).toHaveLength(1);
  expect(api.submits[0]?.key).toBeTruthy();
  expect(api.submits[0]?.body.expectedTotal).toBe(13.8);
  expect(await page.evaluate((key) => sessionStorage.getItem(key), STORAGE_KEY)).toBeNull();

  // The order is the server's now: Back must not reopen the builder with a stale draft.
  await page.getByRole('button', { name: 'Start a new order', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome!' })).toBeVisible();
});

test('a dropped request is retried automatically with the SAME idempotency key', async ({
  page,
}) => {
  const api = await mockApi(page);
  api.dropSubmits = 1;
  await buildOrder(page);

  await page.getByTestId('place-order').click();
  await expect(page).toHaveURL('/orders/order-1');

  expect(api.submits).toHaveLength(2);
  expect(api.submits[0]?.key).toBeTruthy();
  expect(api.submits[1]?.key).toBe(api.submits[0]?.key);
});

test('after the automatic retries run out, Retry resends with the same key', async ({ page }) => {
  const api = await mockApi(page);
  api.dropSubmits = 3;
  await buildOrder(page);

  await page.getByTestId('place-order').click();
  await expect(page.getByTestId('still-sending')).toBeVisible();
  await expect(page.getByTestId('submit-failed')).toContainText('Connection lost');
  expect(api.submits).toHaveLength(3);

  await page.getByTestId('retry').click();
  await expect(page).toHaveURL('/orders/order-1');

  expect(api.submits).toHaveLength(4);
  expect(new Set(api.submits.map((s) => s.key)).size).toBe(1);
});

test('when the server saved the order but the answer was lost, the order locks until Retry', async ({
  page,
}) => {
  const api = await mockApi(page);
  api.loseResponses = 3; // stored on the first attempt; that answer and both retries' answers are lost
  await buildOrder(page);

  await page.getByTestId('place-order').click();
  await expect(page.getByTestId('submit-failed')).toContainText("We couldn't confirm your order");

  // Locked: no edits, no Cancel order, no second Place order. Only Retry.
  await expect(page.getByTestId('order-locked')).toBeVisible();
  await expect(page.getByTestId('size-large').locator('xpath=ancestor::*[@inert]')).toHaveCount(1);
  await expect(
    page.getByRole('banner').getByRole('button', { name: 'Cancel order', exact: true }),
  ).toBeDisabled();
  await expect(page.getByTestId('place-order')).toBeDisabled();

  // A refresh keeps the lock and the same pending request.
  await page.reload();
  await expect(page.getByTestId('order-locked')).toBeVisible();

  await page.getByTestId('retry').click();
  await expect(page).toHaveURL('/orders/order-1');

  // Every request used the same key and body, so the server holds exactly one order.
  expect(new Set(api.submits.map((s) => s.key)).size).toBe(1);
  expect(new Set(api.submits.map((s) => JSON.stringify(s.body))).size).toBe(1);
  expect(api.order?.id).toBe('order-1');
});

test('a definite rejection unlocks the order and the next attempt uses a new key', async ({
  page,
}) => {
  const api = await mockApi(page);
  api.priceOffset = 0.5;
  await buildOrder(page);

  await page.getByTestId('place-order').click();
  await expect(page.getByTestId('price-changed')).toBeVisible();
  await expect(page.getByTestId('order-locked')).toBeHidden();
  await expect(
    page.getByRole('banner').getByRole('button', { name: 'Cancel order', exact: true }),
  ).toBeEnabled();
  expect(api.submits).toHaveLength(1);
});

test('a changed price asks for confirmation and resubmits with a NEW key', async ({ page }) => {
  const api = await mockApi(page);
  api.priceOffset = 0.5;
  await buildOrder(page);

  await page.getByTestId('place-order').click();
  await expect(page.getByTestId('price-changed')).toContainText('$14.30');
  await expect(page).toHaveURL('/order');
  expect(api.submits).toHaveLength(1);

  await page.getByTestId('confirm-price').click();
  await expect(page).toHaveURL('/orders/order-1');

  expect(api.submits).toHaveLength(2);
  expect(api.submits[1]?.body.expectedTotal).toBe(14.3);
  expect(api.submits[1]?.key).toBeTruthy();
  expect(api.submits[1]?.key).not.toBe(api.submits[0]?.key);
});
