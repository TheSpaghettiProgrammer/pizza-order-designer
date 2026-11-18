import { expect, test, type Page } from '@playwright/test';
import { mockApi } from './support/mock-api';

// The order builder against a mocked API: menu, customizer and the line list (#26).

const startButton = (p: Page) => p.getByRole('button', { name: 'Start order', exact: true });
const addToOrder = (p: Page) => p.getByTestId('add-to-order');

async function startOrder(page: Page): Promise<void> {
  await page.goto('/');
  await startButton(page).click();
  await expect(page).toHaveURL('/order');
}

test('the menu comes from the server and shows per-size topping prices', async ({ page }) => {
  await mockApi(page);
  await startOrder(page);

  await expect(page.getByTestId('size-large')).toBeVisible();
  await expect(addToOrder(page)).toBeDisabled();
  await expect(page.getByText('Pick a size to see topping prices.')).toBeVisible();

  await page.getByTestId('size-large').click();
  await expect(page.getByText('Pepperoni (+$0.96)')).toBeVisible();
  await expect(page.getByText('Bacon (+$1.32)')).toBeVisible();
  await page.getByTestId('size-small').click();
  await expect(page.getByText('Pepperoni (+$0.40)')).toBeVisible();
});

test('build a pizza, add it, change it and remove it', async ({ page }) => {
  await mockApi(page);
  await startOrder(page);

  await page.getByTestId('size-large').click();
  await page.getByTestId('topping-pepperoni').click();
  await page.getByRole('button', { name: 'More pizzas to add', exact: true }).click();
  await addToOrder(page).click();

  const line = page.getByTestId('order-line');
  await expect(line).toHaveCount(1);
  await expect(line).toContainText('Large pizza');
  await expect(line).toContainText('Pepperoni');
  await expect(page.getByText('2 pizzas in your order')).toBeVisible();

  // Edit: switch to medium with bacon.
  await line.getByRole('button', { name: 'Edit pizza', exact: true }).click();
  await expect(page.getByText('Change this pizza')).toBeVisible();
  await page.getByTestId('size-medium').click();
  await page.getByTestId('topping-pepperoni').click();
  await page.getByTestId('topping-bacon').click();
  await page.getByRole('button', { name: 'Save changes', exact: true }).click();

  await expect(line).toHaveCount(1);
  await expect(line).toContainText('Medium pizza');
  await expect(line).toContainText('Bacon');
  await expect(line).not.toContainText('Pepperoni');

  await line.getByRole('button', { name: 'Remove pizza', exact: true }).click();
  await expect(page.getByText('No pizzas in your order yet.')).toBeVisible();
});

test('a failing menu load shows a friendly message with a reference, not raw JSON', async ({
  page,
}) => {
  const api = await mockApi(page);
  api.menuFails = true;
  await startOrder(page);

  await expect(page.getByText('Something went wrong. Please try again.')).toBeVisible();
  await expect(page.getByText('Reference: trace-server.error')).toBeVisible();
  await expect(page.getByText('boom')).toHaveCount(0);
});
