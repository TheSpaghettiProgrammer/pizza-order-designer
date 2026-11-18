import { expect, test, type Page } from '@playwright/test';

// The kiosk flow: start screen, order builder shell and the route guard. Each test gets a fresh
// browser context, so sessionStorage starts empty. Seeding uses the literal key and schema version
// on purpose: if either changes, these tests fail loudly.

const STORAGE_KEY = 'pizza-order-designer:draft-order';

const startButton = (p: Page) => p.getByRole('button', { name: 'Start order', exact: true });
const cancelButton = (p: Page) =>
  p.getByRole('banner').getByRole('button', { name: 'Cancel order', exact: true });
const dialog = (p: Page) => p.getByRole('dialog');
const keepButton = (p: Page) =>
  dialog(p).getByRole('button', { name: 'Keep my order', exact: true });
const confirmButton = (p: Page) =>
  dialog(p).getByRole('button', { name: 'Yes, cancel order', exact: true });

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
  await expect(cancelButton(page)).toBeHidden();
  await startButton(page).click();

  await expect(page).toHaveURL('/order');
  await expect(page.getByRole('heading', { name: 'Your order' })).toBeVisible();
  await expect(page.getByText('No pizzas in your order yet.')).toBeVisible();
  await expect(cancelButton(page)).toBeVisible();
});

test('declining the Cancel order confirmation keeps the order', async ({ page }) => {
  await page.goto('/');
  await startButton(page).click();
  await expect(page).toHaveURL('/order');

  await cancelButton(page).click();
  await expect(dialog(page)).toBeVisible();
  await keepButton(page).click();
  await expect(dialog(page)).toBeHidden();
  await expect(page).toHaveURL('/order');

  await cancelButton(page).click();
  await expect(dialog(page)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog(page)).toBeHidden();
  await expect(page).toHaveURL('/order');

  // Focus starts on "Keep my order" (D-044), so a stray Enter keeps the order.
  await cancelButton(page).click();
  await expect(keepButton(page)).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(dialog(page)).toBeHidden();
  await expect(page).toHaveURL('/order');

  await page.reload();
  await expect(page).toHaveURL('/order');
});

test('confirming Cancel order clears the order and returns to the start screen', async ({
  page,
}) => {
  await page.goto('/');
  await startButton(page).click();
  await expect(page).toHaveURL('/order');

  await cancelButton(page).click();
  await confirmButton(page).click();

  await expect(page).toHaveURL('/');
  await expect(startButton(page)).toBeVisible();
  expect(await page.evaluate((key) => sessionStorage.getItem(key), STORAGE_KEY)).toBeNull();

  // Back must not revive the cancelled order.
  await page.goBack();
  await expect(page).toHaveURL('/');
  await expect(startButton(page)).toBeVisible();

  // Start over gives a fresh, empty order.
  await startButton(page).click();
  await expect(page).toHaveURL('/order');
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

test.describe('at tablet width', () => {
  test.use({ viewport: { width: 768, height: 1024 }, hasTouch: true });

  test('Start order and Cancel order are large touch targets', async ({ page }) => {
    await page.goto('/');
    const start = await startButton(page).boundingBox();
    expect(start).not.toBeNull();
    expect(start?.height).toBeGreaterThanOrEqual(96);
    expect((start?.x ?? 0) + (start?.width ?? 0)).toBeLessThanOrEqual(768);

    await startButton(page).tap();
    await expect(page).toHaveURL('/order');
    await expect(cancelButton(page)).toBeVisible();
    const cancel = await cancelButton(page).boundingBox();
    expect(cancel?.height).toBeGreaterThanOrEqual(48);
  });
});
