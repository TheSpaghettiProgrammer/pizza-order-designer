import { expect, test, type Page } from '@playwright/test';

// The kiosk flow: start screen, order builder shell and the route guard. Each test gets a fresh
// browser context, so sessionStorage starts empty. Seeding uses the literal key and schema version
// on purpose: if either changes, these tests fail loudly.

const STORAGE_KEY = 'pizza-order-designer:draft-order';

const startButton = (p: Page) => p.getByRole('button', { name: 'Start order', exact: true });

const draftWithPizzas = (quantity: number) => ({
  version: 1,
  draft: {
    lines: [{ id: 'seed-1', sizeCode: 'large', toppingCodes: ['pepperoni'], quantity }],
    promoCode: null,
    tip: null,
  },
});

async function seedDraft(page: Page, value: unknown): Promise<void> {
  await page.evaluate(([key, json]) => sessionStorage.setItem(key, json), [
    STORAGE_KEY,
    JSON.stringify(value),
  ] as const);
}

test('the start screen leads into an empty order', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Welcome!' })).toBeVisible();
  await startButton(page).click();

  await expect(page).toHaveURL('/order');
  await expect(page.getByRole('heading', { name: 'Your order' })).toBeVisible();
  await expect(page.getByText('No pizzas in your order yet.')).toBeVisible();
});

test('a refresh keeps the order, restored from session storage', async ({ page }) => {
  await page.goto('/');
  await startButton(page).click();
  await expect(page).toHaveURL('/order');

  await seedDraft(page, draftWithPizzas(2));
  await page.reload();

  await expect(page).toHaveURL('/order');
  await expect(page.getByText('2 pizzas in your order')).toBeVisible();
});

test('the order page needs an open order', async ({ page }) => {
  await page.goto('/order');

  await expect(page).toHaveURL('/');
  await expect(startButton(page)).toBeVisible();
});

test('while an order is open the start screen and the 404 Go Home lead back to it', async ({
  page,
}) => {
  await page.goto('/');
  await startButton(page).click();
  await expect(page).toHaveURL('/order');

  await page.goto('/');
  await expect(page).toHaveURL('/order');

  await page.goto('/no/such/page');
  await page.getByRole('link', { name: 'Go Home' }).click();
  await expect(page).toHaveURL('/order');
});

test('corrupt stored data is ignored and removed', async ({ page }) => {
  await page.addInitScript(
    ([key]) => {
      if (!sessionStorage.getItem('seeded')) {
        sessionStorage.setItem('seeded', '1');
        sessionStorage.setItem(key as string, '{not json');
      }
    },
    [STORAGE_KEY],
  );

  await page.goto('/order');

  await expect(page).toHaveURL('/');
  expect(await page.evaluate((key) => sessionStorage.getItem(key), STORAGE_KEY)).toBeNull();
  await startButton(page).click();
  await expect(page).toHaveURL('/order');
});
