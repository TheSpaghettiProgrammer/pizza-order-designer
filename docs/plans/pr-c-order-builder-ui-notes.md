# PR C: order builder UI, implementation notes

Batched PR (D-046), no planning pass. Branch `story/26-32-order-builder-ui`. Stories: #32, #26, #27, #28, #30, #31.

## Commits (one per story)

| Story | Subject |
|---|---|
| #32 | Add API client with friendly error mapping and Notify banners |
| #26 | Add menu browsing and pizza customizer |
| #27 | Add order summary with debounced server quote |
| #28 | Add tip and promo code inputs with inline server validation |
| #30 | Add retry-safe order submit and confirmation screen |
| #31 | Add order status tracking and cancel while Not Started |

## Design notes

- `src/api/`: `apiRequest` (fetch + 8 s timeout via AbortController) maps every failure to `ApiError`
  (`problem | network | timeout | unexpected`); `friendlyMessage`/`describeError` give customer text plus the
  `traceId` as a reference code. Field errors are matched by path prefix (`promoCode`, `tip`).
- Quote store: 300 ms debounce, a sequence number so only the latest answer is kept, "Updating…" state.
- Idempotency key is tied to the request BODY (signature of draft + `expectedTotal`): automatic retries (network/timeout
  only, 3 attempts, 500 ms x n backoff) and the manual Retry reuse it; a confirmed `price.changed` total changes
  `expectedTotal`, so it gets a new key automatically. A key is also dropped after a non-transient 4xx.
- `newId()` falls back to `getRandomValues`/`Math.random` where `crypto.randomUUID` is missing (plain HTTP).
- Playwright specs mock `/api/*` with `page.route` (`test/e2e/support/mock-api.ts`). The mock does simple cent
  arithmetic; that is test code only. Money in the UI is only formatted.

## Deviations

- `test/e2e/kiosk-start.spec.ts`: added a `beforeEach` that mocks the API (the order page now loads the menu).
  Assertions unchanged; OrderPage keeps the "No pizzas in your order yet." / "N pizzas in your order" copy.
- Quantity +/- and remove on line items landed with #26 (they share the line list); #27 added the quote/total.
- Added `Notify` to the Quasar plugins in `quasar.config.ts` (#32).
- The idempotency key lives in memory (checkout store), not sessionStorage: a page refresh mid-submit starts a new
  attempt with a new key. The server's `order.cancelled`/dedupe rules still hold; a refresh during an in-flight submit
  could therefore double-submit. Persisting the key + body signature is a follow-up.
- Cancel on the status page treats a 404 as "already cancelled" (D-040).

## Cut / not done

- #31 **Edit of a placed order** (PUT with `If-Match`, reusing the builder): not implemented. Status polling,
  progress indicator and Cancel (with 409 handling) are done. The PUT 409/412 handling is therefore also untested here.
- No idle-timeout, no offline banner beyond the per-request messages.
- Quote errors that are not tied to `promoCode`/`tip` show a single general message in the price card (no per-line
  field mapping for `lines[i]` paths).

## Checks (from `client/`)

See the final report for exact results.
