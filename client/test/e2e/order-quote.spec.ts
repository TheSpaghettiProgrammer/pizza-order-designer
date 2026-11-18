import { expect, test, type Page } from '@playwright/test';
import { mockApi } from './support/mock-api';

// Running total, breakdown and description (#27). Money shown here is whatever the mocked server said.

const startButton = (p: Page) => p.getByRole('button', { name: 'Start order', exact: true });

async function startWithLargePepperoni(page: Page): Promise<void> {
  await page.goto('/');
  await startButton(page).click();
  await page.getByTestId('size-large').click();
  await page.getByTestId('topping-pepperoni').click();
  await page.getByTestId('add-to-order').click();
}

test('the total appears from the quote and follows quantity changes', async ({ page }) => {
  const api = await mockApi(page);
  await page.goto('/');
  await startButton(page).click();
  await expect(page.getByText('Add a pizza to see the price.')).toBeVisible();
  expect(api.quoteCalls).toHaveLength(0);

  await page.getByTestId('size-large').click();
  await page.getByTestId('topping-pepperoni').click();
  await page.getByTestId('add-to-order').click();

  // Large $12.00 + pepperoni $0.96 = $12.96, tax $0.84 -> $13.80 (from the mocked server).
  await expect(page.getByTestId('subtotal')).toHaveText('$12.96');
  await expect(page.getByTestId('tax')).toHaveText('$0.84');
  await expect(page.getByTestId('total')).toHaveText('$13.80');
  await expect(page.getByTestId('updating')).toBeHidden();

  await page
    .getByTestId('order-line')
    .getByRole('button', { name: 'More Large pizzas', exact: true })
    .click();
  await expect(page.getByTestId('subtotal')).toHaveText('$25.92');
  expect(api.quoteCalls.at(-1)?.lines[0]?.quantity).toBe(2);
  // The UI sends codes and quantities only, never prices.
  expect(JSON.stringify(api.quoteCalls.at(-1))).not.toMatch(/price|total/i);
});

test('rapid changes are debounced into few quote requests', async ({ page }) => {
  const api = await mockApi(page);
  await startWithLargePepperoni(page);
  await expect(page.getByTestId('total')).toBeVisible();
  const before = api.quoteCalls.length;

  const more = page
    .getByTestId('order-line')
    .getByRole('button', { name: 'More Large pizzas', exact: true });
  await more.click();
  await more.click();
  await more.click();
  await expect(page.getByTestId('subtotal')).toHaveText('$51.84');
  expect(api.quoteCalls.length - before).toBeLessThanOrEqual(2);
});

test('Describe my order shows the server description', async ({ page }) => {
  await mockApi(page);
  await startWithLargePepperoni(page);
  await expect(page.getByTestId('total')).toBeVisible();

  await expect(page.getByTestId('description')).toBeHidden();
  await page.getByRole('button', { name: 'Describe my order', exact: true }).click();
  await expect(page.getByTestId('description')).toHaveText('1 × Large pizza — Pepperoni');
});

test('removing the last pizza clears the price', async ({ page }) => {
  await mockApi(page);
  await startWithLargePepperoni(page);
  await expect(page.getByTestId('total')).toBeVisible();

  await page.getByRole('button', { name: 'Remove pizza', exact: true }).click();
  await expect(page.getByText('Add a pizza to see the price.')).toBeVisible();
  await expect(page.getByTestId('total')).toHaveCount(0);
});
