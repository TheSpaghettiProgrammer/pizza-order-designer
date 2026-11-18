import { expect, test, type Page } from '@playwright/test';
import { mockApi } from './support/mock-api';

// Tip and promo inputs with inline server validation messages (#28).

const startButton = (p: Page) => p.getByRole('button', { name: 'Start order', exact: true });

async function startWithLargePepperoni(page: Page): Promise<void> {
  await page.goto('/');
  await startButton(page).click();
  await page.getByTestId('size-large').click();
  await page.getByTestId('topping-pepperoni').click();
  await page.getByTestId('add-to-order').click();
  await expect(page.getByTestId('total')).toHaveText('$13.80');
}

test('tip presets, custom amount and no tip all go through the quote', async ({ page }) => {
  const api = await mockApi(page);
  await startWithLargePepperoni(page);

  await page.getByRole('button', { name: '15%', exact: true }).click();
  await expect(page.getByTestId('tip')).toHaveText('$1.94');
  expect(api.quoteCalls.at(-1)?.tip).toEqual({ mode: 'percent', value: '15' });

  await page.getByRole('button', { name: '$', exact: true }).click();
  await expect.poll(() => api.quoteCalls.at(-1)?.tip).toEqual({ mode: 'amount', value: '15' });
  await page.getByTestId('tip-input').fill('2.50');
  await expect(page.getByTestId('tip')).toHaveText('$2.50');
  // The typed text is sent as-is, as a string.
  expect(api.quoteCalls.at(-1)?.tip).toEqual({ mode: 'amount', value: '2.50' });

  await page.getByRole('button', { name: 'No tip', exact: true }).click();
  await expect(page.getByTestId('tip')).toHaveText('$0.00');
  expect(api.quoteCalls.at(-1)?.tip).toBeNull();
});

test('a bad tip shows the server message next to the field', async ({ page }) => {
  await mockApi(page);
  await startWithLargePepperoni(page);

  await page.getByRole('button', { name: '$', exact: true }).click();
  await page.getByTestId('tip-input').fill('-3');
  await expect(page.getByText('The tip cannot be negative.')).toBeVisible();
  await expect(page.getByText('Fix the highlighted field to see the price.')).toBeVisible();

  await page.getByTestId('tip-input').fill('3');
  await expect(page.getByText('The tip cannot be negative.')).toBeHidden();
  await expect(page.getByTestId('tip')).toHaveText('$3.00');
});

test('apply, reject and remove a promo code', async ({ page }) => {
  await mockApi(page);
  await startWithLargePepperoni(page);
  const apply = page.getByRole('button', { name: 'Apply', exact: true });

  await page.getByTestId('promo-input').fill('NOPE');
  await apply.click();
  await expect(page.getByText('That promo code is not valid.')).toBeVisible();

  await page.getByTestId('promo-input').fill('ilovepizza');
  await apply.click();
  await expect(page.getByText('That promo code is not valid.')).toBeHidden();
  await expect(page.getByText('Code ILOVEPIZZA applied.')).toBeVisible();
  await expect(page.getByTestId('discount')).toHaveText('-$1.30');

  await page.getByRole('button', { name: 'Remove', exact: true }).click();
  await expect(page.getByTestId('discount')).toHaveCount(0);
  await expect(page.getByTestId('total')).toHaveText('$13.80');
});
