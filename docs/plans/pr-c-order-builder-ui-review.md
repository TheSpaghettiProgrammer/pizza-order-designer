# PR C: order builder UI, code review

**Stories:** #32, #26, #27, #28, #30, #31 (edit of a placed order cut) · **Branch:** `story/26-32-order-builder-ui` · **Base:** `origin/main` (`93b2479`)
**Process:** D-046 (batched, no plan file). Developer notes: [`pr-c-order-builder-ui-notes.md`](pr-c-order-builder-ui-notes.md).
**Contract checked against:** `docs/API.md` on `main`, plus the PR B (#47) revision on `origin/story/15-22-order-service` and its `OrderProblems.cs` / `OrdersController.cs` (field paths, codes, ProblemDetails shape).

## 4. Code review  *(pizza-reviewer)*

### Round 1
**Verdict:** CHANGES REQUESTED (2 Major, 6 Minor). The API client, error mapping, quote store and money
handling are solid: no money arithmetic anywhere in `src/` (D-039), stale quotes are guarded by a
sequence number, the Idempotency-Key is tied to the body and reused on retries, and price.changed gets
a new key. Both Majors are the same gap: **the order is not locked while a submit is unresolved**, so an
edit or a kiosk Cancel order during or after an ambiguous submit can produce a duplicate order or wipe
the next customer's draft. One small fix covers both.

**Traceability** (23 ACs: 19 ✅, 3 ⚠️, 1 ❌ cut)

| AC | Implemented in | Proven by | Status |
|---|---|---|---|
| #32 Central client maps ProblemDetails: inline field errors or a Notify banner | `api/http.ts` (`toApiError`), `api/api-error.ts` (`friendlyMessage`, `fieldMessages`), `composables/use-notify-error.ts` | `api-client.test.ts` "maps ProblemDetails…", "filters field errors by path prefix"; e2e tip/promo inline errors | ✅ |
| #32 Network / offline / timeout get their own friendly messages | `api-error.ts:61-65`, `http.ts` (AbortController timeout) | `api-client.test.ts` "dropped connection", "slow server"; e2e `order-submit` "Connection lost" | ✅ (offline = network kind; fine) |
| #32 Unexpected → "Something went wrong" + `traceId` reference | `api-error.ts:66-71`, `describeError` | `api-client.test.ts` "server error"; e2e `order-builder` "failing menu load … reference" | ✅ |
| #32 No raw stack traces or JSON | `toApiError` only reads typed fields; 5xx text is never the server's | `api-client.test.ts` "non-JSON error body"; e2e menu failure | ✅ |
| #26 Menu from `GET /api/menu`, nothing hard-coded | `stores/menu-store.ts`, `PizzaCustomizer.vue` | e2e `order-builder` "menu comes from the server" | ✅ |
| #26 Size → toppings by category with per-size price → quantity | `PizzaCustomizer.vue` | same spec | ✅ |
| #26 Add, or edit an existing line (size, toppings, quantity) | `OrderPage.vue` `onSave`, `draft-order-store.updateLine` | e2e "build a pizza, add it, change it and remove it" | ✅ |
| #26 Touch-friendly, tablet/kiosk widths | `size="lg"` buttons, 72px targets, `col-md-*` grid | no tablet-width spec for the builder (only `kiosk-start`) | ⚠️ visual only |
| #27 Lines with edit, remove, qty +/−; ≥1 line to proceed | `OrderLines.vue`, `QuantityStepper.vue`, `SubmitPanel` hidden/disabled with no lines | e2e "build … remove", "removing the last pizza clears the price" | ✅ |
| #27 Total only from quote, ~300 ms debounce, "updating…", stale ignored | `stores/quote-store.ts` (sequence + abort) | `quote-store.test.ts` (3 tests); e2e "rapid changes are debounced" | ✅ |
| #27 Breakdown: subtotal, discount, tax, tip, total | `OrderSummary.vue` (display only) | e2e `order-quote` / `order-tip-promo` assert each row | ✅ |
| #27 Describe my order shows the server description | `OrderSummary.vue` | e2e "Describe my order…" | ✅ |
| #28 Tip $/% toggle, presets, custom, no tip | `TipPromoPanel.vue` (value sent as typed string) | e2e "tip presets, custom amount and no tip" | ✅ |
| #28 Promo enter/apply/remove; discount in breakdown | `TipPromoPanel.vue`, `OrderSummary.vue` | e2e "apply, reject and remove a promo code" | ✅ |
| #28 Server validation inline next to the field | `fieldMessages(…, 'promoCode' / 'tip')` (matches server paths `promoCode`, `tip.value`, `tip.mode`) | e2e "a bad tip shows the server message…", promo reject | ✅ |
| #30 One key per checkout attempt, reused on every retry | `checkout-store.ts:43-50` (`keyFor` by body signature) | e2e "dropped request … SAME key", "Retry resends with the same key" | ⚠️ Major 1 (an edit after an ambiguous failure gets a new key) |
| #30 Timeout + automatic retry with backoff; "Still sending…"; manual retry | `checkout-store.ts:83-95` (3 attempts, 500 ms × n), `SubmitPanel.vue` | e2e `order-submit` (connection reset); timeout kind unit-tested only | ✅ |
| #30 Confirmation with order ID, description, total | `OrderStatusPage.vue` | e2e "placing an order shows the confirmation…" | ✅ |
| #30 409 price changed → show new total, ask to confirm | `checkout-store.ts:99-102`, `quoteStore.adopt`, `SubmitPanel.vue` banner | e2e "a changed price asks for confirmation and resubmits with a NEW key" | ✅ |
| #31 `/orders/:id` polls every 3 s, progress NotStarted → … → Delivered (or Cancelled) | `OrderStatusPage.vue` (setTimeout chain, stops on Delivered/404/unmount); Cancelled is a client-side card per D-040 | e2e "the status page polls and follows the kitchen" | ✅ |
| #31 Cancel shown only while Not Started | `OrderStatusPage.vue` `v-if="order.status === 'NotStarted'"`, confirm dialog | e2e "cancelling while Not Started…" (incl. "Keep my order") | ✅ |
| #31 Edit reuses the builder and sends `If-Match` | not built (cut, see notes) | — | ❌ cut |
| #31 Kitchen starts mid-action (409/412) → friendly message + refresh | cancel 409 `order.already_started` handled; PUT 412 n/a (edit cut) | e2e "if the kitchen starts first, cancel explains and refreshes" | ⚠️ partial (cancel only) |

**Plan conformance** (no plan by design, D-046; deviations from the notes):
- `kiosk-start.spec.ts` `beforeEach` mocks the API: acceptable, assertions unchanged.
- Quantity +/− and remove landed in the #26 commit: acceptable (shared component), ACs still traced.
- `Notify` added to `quasar.config.ts` plugins: acceptable, required by #32.
- Idempotency key in memory only: accepted as a known limitation, Minor 1. It becomes cheap to fix together with Major 1.
- 404 on cancel = already cancelled (D-040): correct in code (`OrderStatusPage.vue:195`), but untested (Minor 6).
- Cut: #31 edit (PUT + If-Match + 409/412), idle timeout, line-level error mapping. All in line with D-046's "cut C before B". Put the #31 edit cut in the PR body and keep #31 open (or `Refs`, not `Closes`).
- Commits: 6, one per story, each `Refs #N` and `Co-Authored-By: Claude Sonnet 5.5`. No new dependency (`package.json` untouched). No unrelated churn.

**Contract fidelity: API client vs `docs/API.md` (main + PR B)**
- Client: paths, methods, `Idempotency-Key` header, body (`lines/sizeCode/toppingCodes/quantity`, `promoCode`, `tip{mode,value:string}`, `expectedTotal` number), 200/201 both success, 204 handled with no body read, ETag captured, `code` / `errors` / `traceId` / `quote` read from the top level as ASP.NET serializes extensions. Matches.
- Mock (`test/e2e/support/mock-api.ts`): field names, money as numbers, ProblemDetails shape, `quote` extension on 409, 204 on DELETE, 409 `order.already_started`, 404 `order.not_found`, quoted ETag on GET all match. Drifts in Minor 4 (none invalidates the current assertions, because the key tests assert key equality and inequality explicitly).

**Money-math ban (D-039):** `grep` over `src/`: the only arithmetic is on quantities (`QuantityStepper` ±1, `itemCount` sum). Money is only passed to `formatMoney`. `-${formatMoney(discount)}` is a string prefix. ✅

**Kiosk guard and `/orders/:id`:** not a building route, so no Cancel order in the header. `place()` clears the draft before pushing, so the guard can't bounce it, and "Start a new order" lands on `/`. A refresh on the status page works (the id comes from the route). ✅ The problem is the reverse direction, Major 2.

**Checks re-run** (from `client/`, in this worktree):

| Command | Result |
|---|---|
| `npm run typecheck` | ✅ clean |
| `npx eslint -c ./eslint.config.js "./src*/**/*.{ts,js,cjs,mjs,vue}" test/e2e test/vitest` | ✅ exit 0, no output |
| `npm test` (Vitest) | ✅ 5 files, **42/42** passed |
| `npm run test:e2e` (Playwright, own server on :9100) | ✅ **27/27** passed (19.2 s) |
| `npx prettier --check --end-of-line auto <all 30 changed client files>` | ✅ all formatted |
| `npx quasar build` | not re-run (not in the brief's list) |

Not done: a mutation run to show the tests fail without the change (no code edits allowed). By reading, the key tests would catch a broken `keyFor` (they assert `toBe` / `not.toBe` on keys), and `quote-store.test.ts` would catch a missing sequence check.

#### Findings

**Major 1: an edit after an ambiguous submit failure gets a new Idempotency-Key, so the order can be placed twice.**
`src/stores/checkout-store.ts:43-50` (`keyFor`), `:117-126` (the watch returns the phase to `idle` on any edit), `src/pages/OrderPage.vue:14-37` (customizer, lines and tip/promo stay live while `sending` / `failed`).
*Scenario:* the customer taps Place order on a flaky kiosk connection. The request reaches the server and the order is created, but the response is lost, and all 3 attempts end in network/timeout errors ("Connection lost… Retry"). Instead of tapping Retry, the customer bumps the tip from 15% to 20% (or changes a quantity) and taps Place order. The body signature changed, so `keyFor` mints a new key. The server has never seen it, so **a second order goes to the kitchen**. This is the exact case #30 exists for ("I never place a duplicate order"). A milder variant: an edit made while "Still sending…" is silently dropped, because the in-flight body is placed and the draft then cleared.
*Fix:* while `phase === 'sending'`, and after a failure whose outcome is unknown (transient, or 5xx), lock the order: disable the customizer, line steppers/edit/remove and tip/promo, and offer only **Retry** (same key) or Cancel order. Only clear the lock on a definite answer (2xx, or a 4xx that proves nothing was created). Add an e2e test: drop 3 submits, check the builder is disabled and Retry reuses the key.

**Major 2: Cancel order during an in-flight submit leaves an orphaned `place()` that later wipes the next customer's draft and jumps to the old order.**
`src/components/SubmitPanel.vue:79-87`, `src/composables/use-end-order.ts:11-14` (does not reset the checkout/quote stores), `src/layouts/MainLayout.vue:6` (Cancel order stays visible while sending).
*Scenario:* customer A taps Place order; "Still sending…" runs (up to ~26 s: 3 × 8 s timeout + backoff). A gives up, taps Cancel order and confirms, and the kiosk shows the start screen. Customer B taps Start order and adds a pizza (B's Place order shows a spinner, because `checkout.phase` is still `sending`). A's request then succeeds. The still-running `place()` calls `draftStore.cancelOrder()`, which **deletes B's draft**, and routes the kiosk to A's order status page. A also believes they cancelled, but the order is in the kitchen.
*Fix (pairs with Major 1):* hide or disable Cancel order while `checkout.phase === 'sending'`. As a belt-and-braces guard, have `endOrder()` call `checkout.reset()` / `quote.reset()`, and have `place()` only clear and navigate if the draft it submitted is still the current one (for example a generation counter that `reset()` bumps and `submit()` checks before returning the order).

**Minor 1: the idempotency key lives in memory only** (`checkout-store.ts:36-37`). Do a refresh mid-submit or after an ambiguous failure, and the restored draft gets a new key, which is a possible duplicate. The developer flagged it. Rare on a kiosk, so Minor, and acceptable for this PR. It's cheap to fix with Major 1: persist `{ key, bodySignature, phase }` next to the draft in the existing sessionStorage envelope (D-045). Otherwise list it in the README trade-offs.

**Minor 2: a 2xx whose body fails to read becomes success with `data: undefined`** (`src/api/http.ts:86-95`). If the 8 s timer fires while `response.json()` is still streaming (headers already arrived), the catch sets `payload = undefined` and `response.ok` returns `{ data: undefined }`. For submit, `checkout.submit()` then returns `undefined`, `forgetKey()` has already run, and `place()` silently does nothing. A second tap uses a new key, so a duplicate order. *Fix:* on a 2xx with an expected body, treat a body-read failure as `timeout` / `unexpected` (throw), so the key is kept and Retry stays available.

**Minor 3: `friendlyMessage` shows `detail` instead of `title`, and the server splits them as headline + explanation** (`src/api/api-error.ts:70`). With PR B's wording, a 404 on the status page reads only "It may have been cancelled.", and a 412 reads only "Refresh to see the latest version, then try again." The mock sets `title === detail` (`mock-api.ts:128-137`), so tests can't see this. *Fix:* show `title` plus `detail` when both are present (e.g. "We couldn't find that order. It may have been cancelled.").

**Minor 4: mock drift from the contract** (`test/e2e/support/mock-api.ts`):
- `:204-205`: same key + **different** body returns the original order (200). The contract says **422** `idempotency.key_reused`. A key-reuse bug would show as success in the UI. The explicit key assertions save the current tests, but future tests won't be.
- `:200-203`: a dropped submit is aborted **before** the order is stored. "Server committed, response lost", the case idempotency really protects, is never simulated. Add a `dropResponses` mode that stores first, then aborts.
- Codes `validation` / `internal` versus the real `order.invalid` / `server.error`. No `ETag` on the POST response. `409 order.cancelled` isn't modelled. The UI doesn't branch on these today, so this is low impact.

**Minor 5: status page race after cancel** (`src/pages/OrderStatusPage.vue:147-154` vs `:210`). A poll that is in flight when the DELETE succeeds can resolve 404 after `markCancelled()`, and `refresh()` then sets `error`. The red "could not find that order" banner appears above the "Your order has been cancelled" card. *Fix:* in `refresh()`, ignore results once `cancelled` is true (or bump a token in `markCancelled`).

**Minor 6: test gaps and small nits.**
- No test for D-040's 404-on-cancel = "already cancelled" (`OrderStatusPage.vue:195`).
- No tablet-width check for the builder (#26 AC4).
- `otherFieldMessages` (`api-error.ts:95`) is unused.
- The "Still sending… Connection lost, retrying." copy (`SubmitPanel.vue:40`) also shows when the server is merely slow (after 3 s, attempt 1). "Still sending…" alone is accurate.

---

## Response and outcome  *(orchestrator)*

Fixed by the orchestrator in "Address PR C review" and verified directly (D-046: one review pass per PR).

| # | Finding | Resolution |
|---|---|---|
| M1 | After an unclear failure, an edit plus Place order minted a new key and could place a 2nd order | **D-048:** a new `outcomeUnclear` state (network error, timeout, 5xx, or an unreadable 2xx body). While a submit is in flight or unclear, `locked` makes the builder, the line list and tip/promo **inert**, disables Place order, and leaves **Retry** (the exact same body + key). Edits only reset the checkout after a *definite* rejection. |
| M2 | Cancel order during an in-flight submit; the store wasn't reset | Cancel order is disabled while `locked`. `endOrder()` refuses while locked and resets the quote and checkout stores. |
| Minor 1 | Key only in memory | The pending `{key, body}` is persisted in sessionStorage. A refresh restores the locked state, and Retry resends the same request. Tested (reload in the lost-response spec). |
| Minor 2 | An unreadable 2xx body counted as success | `http.ts` now throws a transient error, so the outcome is unclear and the order stays locked → Retry. |
| Minor 3 | Detail shown without the title | Messages show "title. detail". |
| Minor 4 | Mock drift | The mock now uses the real codes (`order.invalid`, `server.error`), 422 for key reuse, 409 `order.cancelled`, an ETag on POST, separate title/detail, and **`loseResponses`** (the server stores the order, then the answer is lost). |
| Minor 5 | Poll vs cancel banner | Not fixed (cosmetic). Listed in the notes. |
| Minor 6 | Test gaps | New: the lost-response spec (locked UI, Cancel disabled, reload keeps the lock, Retry → one order, one key, one body) and the definite-rejection-unlocks spec. 404-on-cancel and the tablet builder aren't added (time). |

**Mutation check:** forgetting the key after an unclear failure fails **2** specs, the new lost-response spec
and the existing "Retry resends with the same key".

**Re-run (orchestrator):** typecheck ✅, ESLint ✅, Prettier (changed files) ✅, Vitest **42/42**,
Playwright **29/29**.

**Also verified end to end against the real API** (PR B's API + this UI, real Postgres): menu → 2 × Large
pepperoni+bacon → ILOVEPIZZA + 15% → **$31.23** (matches the contract example) → Place order → status page
→ a kitchen advance shows "Preparing", and Cancel is hidden.
