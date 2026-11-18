import type { Page, Route } from '@playwright/test';

// A tiny in-test stand-in for the order service, so the UI specs are deterministic and need no
// backend. The arithmetic here is TEST code (the real UI never does money maths, D-039).

interface DraftBody {
  lines: { sizeCode: string; toppingCodes: string[]; quantity: number }[];
  promoCode: string | null;
  tip: { mode: 'amount' | 'percent'; value: string } | null;
  expectedTotal?: number;
}

type Status = 'NotStarted' | 'Started' | 'ReadyForPickup' | 'Delivered';

interface StoredOrder {
  id: string;
  status: Status;
  version: number;
  pricing: Record<string, unknown>;
}

export const MENU = {
  sizes: [
    { code: 'small', name: 'Small', basePrice: 5 },
    { code: 'medium', name: 'Medium', basePrice: 7 },
    { code: 'large', name: 'Large', basePrice: 12 },
  ],
  toppingCategories: [
    {
      code: 'basic',
      name: 'Basic',
      rate: 0.08,
      toppings: [
        {
          code: 'pepperoni',
          name: 'Pepperoni',
          pricesBySize: { small: 0.4, medium: 0.56, large: 0.96 },
        },
      ],
    },
    {
      code: 'premium',
      name: 'Premium',
      rate: 0.11,
      toppings: [
        { code: 'bacon', name: 'Bacon', pricesBySize: { small: 0.55, medium: 0.77, large: 1.32 } },
      ],
    },
  ],
  taxRate: 0.065,
  tipBase: 'PreTax',
};

const cents = (n: number): number => Math.round(n * 100);
const money = (c: number): number => c / 100;

export function priceDraft(body: DraftBody): Record<string, unknown> {
  let subtotal = 0;
  const lines = body.lines.map((l) => {
    const size = MENU.sizes.find((s) => s.code === l.sizeCode)!;
    const toppings = l.toppingCodes.map((code) => {
      const cat = MENU.toppingCategories.find((c) => c.toppings.some((t) => t.code === code))!;
      const t = cat.toppings.find((x) => x.code === code)!;
      return {
        code,
        name: t.name,
        categoryCode: cat.code,
        price: t.pricesBySize[l.sizeCode as 'small'],
      };
    });
    const unit = cents(size.basePrice) + toppings.reduce((sum, t) => sum + cents(t.price), 0);
    subtotal += unit * l.quantity;
    const names = toppings.map((t) => t.name).join(', ');
    return {
      sizeCode: l.sizeCode,
      sizeName: size.name,
      quantity: l.quantity,
      basePrice: size.basePrice,
      toppings,
      unitPrice: money(unit),
      lineTotal: money(unit * l.quantity),
      description: `${l.quantity} × ${size.name} pizza${names ? ' — ' + names : ''}`,
    };
  });
  const discount = body.promoCode?.toUpperCase() === 'ILOVEPIZZA' ? Math.round(subtotal * 0.1) : 0;
  const tax = Math.round((subtotal - discount) * 0.065);
  let tip = 0;
  if (body.tip) {
    const v = Number(body.tip.value);
    tip = body.tip.mode === 'percent' ? Math.round(((subtotal - discount) * v) / 100) : cents(v);
  }
  return {
    lines,
    subtotal: money(subtotal),
    promoCode: discount ? 'ILOVEPIZZA' : null,
    discount: money(discount),
    taxRate: 0.065,
    tax: money(tax),
    tipBase: 'PreTax',
    tip: money(tip),
    total: money(subtotal - discount + tax + tip),
    description: lines.map((l) => l.description).join('\n'),
  };
}

export interface MockApiState {
  quoteCalls: DraftBody[];
  submits: { key: string | undefined; body: DraftBody }[];
  /** Abort this many submit requests (connection reset) BEFORE the server stores anything. */
  dropSubmits: number;
  /** For this many submits, the server STORES the order and then the response is lost. */
  loseResponses: number;
  /** The server's total differs from the draft's by this many dollars (409 price.changed). */
  priceOffset: number;
  order: StoredOrder | null;
  menuFails: boolean;
}

function json(route: Route, status: number, body: unknown, headers: Record<string, string> = {}) {
  return route.fulfill({
    status,
    headers: {
      'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json',
      ...headers,
    },
    body: JSON.stringify(body),
  });
}

// Same shape as the real API (docs/API.md): a headline title, a "what now" detail, stable code, traceId.
function problem(
  route: Route,
  status: number,
  code: string,
  title: string,
  detail?: string,
  extra = {},
) {
  return json(route, status, {
    title,
    ...(detail ? { detail } : {}),
    status,
    code,
    traceId: 'trace-' + code,
    ...extra,
  });
}

export async function mockApi(page: Page): Promise<MockApiState> {
  const state: MockApiState = {
    quoteCalls: [],
    submits: [],
    dropSubmits: 0,
    loseResponses: 0,
    priceOffset: 0,
    order: null,
    menuFails: false,
  };
  const byKey = new Map<string, { order: StoredOrder; body: string }>();
  const deleted = new Set<string>();
  let created = 0;

  const orderJson = (o: StoredOrder) => ({
    id: o.id,
    status: o.status,
    createdAt: '2026-11-17T21:30:00Z',
    updatedAt: '2026-11-17T21:30:00Z',
    version: String(o.version),
    pricing: o.pricing,
  });

  await page.route(
    (url) => url.pathname.startsWith('/api/'),
    async (route) => {
      const request = route.request();
      const url = new URL(request.url());
      const path = url.pathname;
      const method = request.method();

      if (path === '/api/menu') {
        if (state.menuFails) {
          return problem(
            route,
            500,
            'server.error',
            'Something went wrong on our side.',
            'Please try again.',
          );
        }
        return json(route, 200, MENU);
      }

      if (path === '/api/orders/quote' && method === 'POST') {
        const body = request.postDataJSON() as DraftBody;
        state.quoteCalls.push(body);
        if (body.promoCode && body.promoCode.toUpperCase() !== 'ILOVEPIZZA') {
          return json(route, 400, {
            title: 'Please check your order.',
            detail: 'Some items in your order need attention.',
            status: 400,
            code: 'order.invalid',
            errors: { promoCode: ['That promo code is not valid.'] },
          });
        }
        if (body.tip && Number(body.tip.value) < 0) {
          return json(route, 400, {
            title: 'Please check your order.',
            detail: 'Some items in your order need attention.',
            status: 400,
            code: 'order.invalid',
            errors: { 'tip.value': ['The tip cannot be negative.'] },
          });
        }
        return json(route, 200, priceDraft(body));
      }

      if (path === '/api/orders' && method === 'POST') {
        const key = request.headers()['idempotency-key'];
        const body = request.postDataJSON() as DraftBody;
        state.submits.push({ key, body });
        if (state.dropSubmits > 0) {
          state.dropSubmits -= 1;
          return route.abort('connectionreset');
        }
        if (!key) {
          return problem(route, 400, 'idempotency.key_missing', "We couldn't send your order.");
        }
        const bodyJson = JSON.stringify(body);
        const existing = byKey.get(key);
        if (existing) {
          if (existing.body !== bodyJson) {
            return problem(
              route,
              422,
              'idempotency.key_reused',
              "This request doesn't match the order it was first sent with.",
            );
          }
          if (deleted.has(existing.order.id)) {
            return problem(route, 409, 'order.cancelled', 'This order was cancelled.');
          }
          if (state.loseResponses > 0) {
            state.loseResponses -= 1;
            return route.abort('connectionreset'); // the replay's answer is lost too
          }
          return json(route, 200, orderJson(existing.order), {
            ETag: `"${existing.order.version}"`,
          });
        }
        const pricing = priceDraft(body);
        const serverTotal = Number(pricing.total) + state.priceOffset;
        if (
          body.expectedTotal !== undefined &&
          Math.abs(body.expectedTotal - serverTotal) > 0.001
        ) {
          return problem(
            route,
            409,
            'price.changed',
            'The price of your order has changed.',
            'Please review and confirm.',
            { quote: { ...pricing, total: Number(serverTotal.toFixed(2)) } },
          );
        }
        created += 1;
        const order: StoredOrder = {
          id: 'order-' + created,
          status: 'NotStarted',
          version: 1,
          pricing,
        };
        byKey.set(key, { order, body: bodyJson });
        state.order = order;
        if (state.loseResponses > 0) {
          state.loseResponses -= 1;
          return route.abort('connectionreset'); // stored, but the customer never hears back
        }
        return json(route, 201, orderJson(order), {
          Location: `/api/orders/${order.id}`,
          ETag: `"${order.version}"`,
        });
      }

      const match = /^\/api\/orders\/([^/]+)$/.exec(path);
      if (match) {
        const order = state.order;
        if (!order || order.id !== match[1]) {
          return problem(
            route,
            404,
            'order.not_found',
            "We couldn't find that order.",
            'It may have been cancelled.',
          );
        }
        if (method === 'GET') {
          return json(route, 200, orderJson(order), { ETag: `"${order.version}"` });
        }
        if (method === 'DELETE') {
          if (order.status !== 'NotStarted') {
            return problem(
              route,
              409,
              'order.already_started',
              'The kitchen has already started your order.',
              'It can no longer be changed or cancelled.',
            );
          }
          deleted.add(order.id);
          state.order = null;
          return route.fulfill({ status: 204 });
        }
      }

      return problem(route, 404, 'not_mocked', `Not mocked: ${method} ${path}`, undefined);
    },
  );

  return state;
}
