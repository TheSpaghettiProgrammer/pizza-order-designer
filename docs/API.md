# Order Service API Contract

The contract between the order builder (Quasar UI) and the order service (.NET API). The UI calls
relative `/api/...` only (D-011). The server is the single source of truth for money (D-039).

**Status:** every endpoint below is implemented.
- Menu and quote: #8, #14.
- Orders and kitchen: #15–#22.

Differences from the first draft of this contract:
- `PUT` without `If-Match` returns **428** `order.precondition_required`.
- A kitchen advance that lost a race to another change returns **409** `order.status_changed`.
- Unexpected errors return **500** `server.error` with a `traceId`.
- Malformed JSON returns **400** `request.invalid`.

Runnable examples are in `server/src/PizzaOrderDesigner.Api/PizzaOrderDesigner.Api.http`.

**Conventions**
- JSON, camelCase. **Money** is a JSON number with exactly two decimals (`12.96`), from C# `decimal`.
  **Rates** are numbers with up to four decimals (`0.065`).
- Menu items are identified by stable **codes** (`large`, `pepperoni`), never by database ids.
- **Tip `value` is a decimal string** (`"2.50"`, `"15"`). The UI sends exactly what the customer typed,
  so no float ever touches money. The server parses it with the invariant culture.
- **Errors** are RFC 7807 ProblemDetails: `title` and `detail` are customer-friendly, `code` is stable
  and machine-readable, `errors` maps field paths to messages, and `traceId` matches the server logs.

## Shared shapes

```jsonc
// OrderDraft: the request body for quote, submit and update
{
  "lines": [
    { "sizeCode": "large", "toppingCodes": ["pepperoni", "bacon"], "quantity": 2 }
  ],
  "promoCode": "ILOVEPIZZA",                // or null. One code per order (D-043)
  "tip": { "mode": "percent", "value": "15" } // or { "mode": "amount", "value": "2.50" }, or null
}

// PriceBreakdown: returned by quote, and inside every Order
{
  "lines": [
    {
      "sizeCode": "large", "sizeName": "Large", "quantity": 2,
      "basePrice": 12.00,
      "toppings": [
        { "code": "pepperoni", "name": "Pepperoni", "categoryCode": "basic", "price": 0.96 },
        { "code": "bacon",     "name": "Bacon",     "categoryCode": "premium", "price": 1.32 }
      ],
      "unitPrice": 14.28,
      "lineTotal": 28.56,
      "description": "2 × Large pizza — Pepperoni, Bacon"
    }
  ],
  "subtotal": 28.56,
  "promoCode": "ILOVEPIZZA",     // normalized code actually applied, or null
  "discount": 2.86,
  "taxRate": 0.065,
  "tax": 1.67,
  "tipBase": "PreTax",           // D-038: PreTax (default) | PostTax
  "tip": 3.86,
  "total": 31.23,
  "description": "2 × Large pizza — Pepperoni, Bacon"   // all lines, one per line (\n)
}
```
Formula (D-038): toppings rounded per topping; `discount` on the subtotal; `tax = round((subtotal −
discount) × taxRate)`, rounded once; tip untaxed; half-up (`MidpointRounding.AwayFromZero`).

## Endpoints

### `GET /api/menu`
Returns everything the builder needs to render choices. Topping prices are precomputed per size.
```jsonc
{
  "sizes": [ { "code": "small", "name": "Small", "basePrice": 5.00 }, /* medium 7.00, large 12.00 */ ],
  "toppingCategories": [
    {
      "code": "basic", "name": "Basic", "rate": 0.08,
      "toppings": [
        { "code": "pepperoni", "name": "Pepperoni",
          "pricesBySize": { "small": 0.40, "medium": 0.56, "large": 0.96 } }
      ]
    },
    { "code": "premium", "name": "Premium", "rate": 0.11, "toppings": [ /* ... */ ] }
  ],
  "taxRate": 0.065,
  "tipBase": "PreTax"
}
```

### `POST /api/orders/quote`
Body: `OrderDraft`. Returns **200** `PriceBreakdown`. Nothing is persisted. The UI calls it (debounced)
on every change and only displays what comes back.
**400** with field errors for invalid input. Examples: unknown size/topping code; quantity outside 1–20;
more than 25 lines or none; a duplicate topping; a bad tip; `promoCode` unknown, ineligible ("SAVE5
needs an order over $20.00 before tax") or conflicting (D-043).

### `POST /api/orders`
Headers: `Idempotency-Key: <uuid>` (required; one per checkout attempt, reused on every retry).
Body: `OrderDraft` plus `"expectedTotal": 31.23` (the total the customer saw).

| Case | Response |
|---|---|
| New key | **201**, `Location: /api/orders/{id}`, body `Order` |
| Same key, same body (a retry) | **200**, the original `Order`. No second order |
| Same key, different body | **422**, `code: idempotency.key_reused` |
| Key whose order was cancelled (deleted) | **409**, `code: order.cancelled`. Never re-created (D-040) |
| Server total ≠ `expectedTotal` | **409**, `code: price.changed`, the extension `quote: PriceBreakdown` |
| Missing key | **400**, `code: idempotency.key_missing` |
| Invalid draft | **400** (as for quote) |

```jsonc
// Order
{
  "id": "3f2c…", "status": "NotStarted",   // NotStarted | Started | ReadyForPickup | Delivered
  "createdAt": "2026-11-17T21:30:00Z", "updatedAt": "2026-11-17T21:30:00Z",
  "version": "1234",                       // also sent as the ETag header
  "pricing": { /* PriceBreakdown snapshot at submit/update */ }
}
```

### `GET /api/orders/{id}`
**200** `Order` (with an `ETag`), or **404** `code: order.not_found`. Safe to poll.

### `PUT /api/orders/{id}`
Headers: `If-Match: "<version>"`. Body: `OrderDraft` plus `expectedTotal`. Only while **NotStarted**.

| Case | Response |
|---|---|
| OK | **200** `Order`, re-priced, with a new version |
| Kitchen already started | **409** `code: order.already_started` |
| Stale version | **412** `code: order.version_mismatch` (re-fetch, then retry). A PUT retried after it already succeeded also gets 412; re-fetching shows the change applied |
| Price changed | **409** `code: price.changed` |
| Unknown or deleted | **404** |

### `DELETE /api/orders/{id}`
Cancel = delete (D-040). Only while **NotStarted**, in one conditional statement.
**204**. **409** `code: order.already_started`. **404** if unknown or already deleted (the UI treats a
404 on a cancel retry as "already cancelled").

### `POST /api/kitchen/orders/{id}/advance`
Restaurant side (D-042): NotStarted → Started → ReadyForPickup → Delivered. **200** `Order`.
**409** `code: order.already_delivered`. **404**. An optional auto-advance timer runs behind the config
key `Kitchen:AutoAdvance:Enabled`.

```bash
curl -X POST http://localhost:5080/api/kitchen/orders/<id>/advance
```

### `GET /api/health`
**200** `Healthy` when the API can reach Postgres.
