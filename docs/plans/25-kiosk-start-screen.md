# #25 — Kiosk start screen: begin, cancel and start over

| | |
|---|---|
| **Story** | https://github.com/TheSpaghettiProgrammer/pizza-order-designer/issues/25 |
| **Branch** | `story/25-kiosk-start-screen`, stacked on `chore/41-playwright-e2e` (PR #42, not merged yet) |
| **Status** | Plan review: round 1 = REVISE. Waiting on the user (idle timeout, defaults) and the D-number fix before planner round 2 |
| **Depends on** | #41 (Playwright harness) |
| **Decisions** | D-011, D-020, D-024, D-025, D-034, D-035 |

> Workflow and rules: [docs/SUBAGENTS.md](../SUBAGENTS.md). Each role writes only its own section.
> Later rounds are appended as `### Round 2`, never overwritten.

---

## 1. Plan  *(pizza-planner)*

### 1.1 Goal and scope
#25 gives the kiosk its first real flow. There's a welcome screen with a large **Start order** button, then an
order-builder page at `/order` that holds a **draft order** in a Pinia store. A **Cancel order** button sits in
the header, asks for confirmation, clears the draft and goes back to the start screen. The draft is saved to
`sessionStorage`, so a page refresh keeps it. The store's types are designed now so that #26 (line items),
#27 (quote request) and #28 (promo code, tip) only **add actions**, not reshape the state. The store holds
**no prices** and does no money math. No backend endpoint is needed.

- **In scope:** AC 1 (welcome screen + Start order), AC 2 (Cancel order, always available while building,
  with confirmation → clears the draft → start screen), AC 3 (draft in Pinia, survives a refresh via
  sessionStorage, and fails safe when storage is broken or unavailable).
- **"Start over"** (story title): read as *cancel, then Start order again gives a fresh, empty draft*. There's
  no separate "Start over" button, because none of the ACs asks for one. Proposed as an assumption (1.7).
- **Deferred: AC 4, the idle timeout (optional, P2).** Recommendation: **defer it to the cut list.**
  - It's optional P2 kiosk polish. The grading weighs pricing, lifecycle and invariant tests, and #1 (scope vs
    the kiosk vision) is still unanswered.
  - Doing it properly isn't free: an activity listener, a "Still there?" countdown dialog so a slow customer
    isn't wiped mid-choice, interaction with the open Cancel dialog, and a Playwright `page.clock` test. That's
    roughly 30–45 min for something the graders don't evaluate.
  - The design keeps it cheap later. A `useIdleTimeout` composable in `MainLayout`, active only on
    `meta.building` routes, would call the same `cancelOrder()` + `router.push({ name: 'start' })` path as
    the Cancel button. Nothing in this plan has to change for it.
- **Out of scope:** line items, the menu, sizes and toppings (#26), totals and the quote (#27), tip and promo
  inputs (#28), cancelling a *submitted* order (#19), quantity/line limits (#7, unanswered; the store
  deliberately enforces none).

### 1.2 Approach

**Draft-order model** (`src/models/draft-order.ts`, types plus one factory, no Vue):
```ts
export interface DraftLine {
  id: string;             // client-side key for edit/remove and v-for; #26 generates it
  sizeCode: string;       // menu code from #8, e.g. 'large' (codes, never DB ids or prices)
  toppingCodes: string[]; // menu codes, e.g. ['pepperoni', 'bacon']
  quantity: number;       // integer; limits are #7's job, validated by the server
}
export type TipMode = 'amount' | 'percent';
export interface TipInput {
  mode: TipMode;
  value: string;          // exactly what the customer entered, e.g. '2.50' or '15'. A string, so no float
}                         // ever touches money. The server parses it as decimal (#12)
export interface DraftOrder {
  lines: DraftLine[];
  promoCode: string | null; // raw input; the server trims/normalizes it (#11)
  tip: TipInput | null;     // null = no tip chosen yet
}
export function createEmptyDraft(): DraftOrder; // { lines: [], promoCode: null, tip: null }
```
- Required fields with `| null` instead of optional properties, because `exactOptionalPropertyTypes` is on in
  `.quasar/tsconfig.json`, and a fixed shape makes the restore validation simple.
- This maps 1:1 to #14's quote body ("lines, promo code, tip") and #12's `{ mode, value }` tip input. #27's
  API client turns it into the request, and `TipInput.value` is sent as a decimal string.
- There are no money fields. The only number is `quantity` (a count). `itemCount` is a sum of quantities.

**Persistence** (`src/stores/draft-order-persistence.ts`, hand-rolled, no new dependency):
- Key `pizza-order-designer:draft-order`. The value is a versioned envelope `{ "version": 1, "draft": DraftOrder }`.
- `loadDraft(): DraftOrder | null`:
  - Reads `globalThis.sessionStorage` **inside** a try/catch, because the property access itself can throw a
    `SecurityError`.
  - Parses the JSON and checks the envelope and shape with a hand-written guard (`parseStoredDraft(raw:
    string | null): DraftOrder | null`): version `=== 1`, `lines` is an array of `{ id: string, sizeCode:
    string, toppingCodes: string[], quantity: number }` (`Number.isInteger`), `promoCode` is a string or null,
    and `tip` is null or `{ mode: 'amount' | 'percent', value: string }`.
  - Returns a **fresh object with only the known fields**, so stray data never reaches the store.
  - Anything unreadable (bad JSON, wrong version, wrong shape) → returns `null` and removes the key
    (best-effort). Any thrown storage error → `null`. It never throws.
- `saveDraft(draft: DraftOrder | null): void`: writes the envelope, or `removeItem` for `null`. On any error
  (quota, unavailable) it swallows the error and logs one `console.warn`. The app carries on in memory only,
  and the only thing lost is refresh survival.
- Why `sessionStorage`, not `localStorage`: a kiosk is shared. Closing the tab or browser must not leave the
  last customer's order behind, while a refresh keeps it (the AC asks for session storage).
- Validation is about **shape only**. Whether a stored `sizeCode`/topping still exists on the menu is checked
  by the server (#10: unknown codes → validation errors) and handled in #26/#27.

**Store** (`src/stores/draft-order-store.ts`, a Pinia **setup** store, `defineStore('draftOrder', () => …)`):
- State: `draft = ref<DraftOrder | null>(loadDraft())`. `null` means no order in progress (the start screen).
  Because it loads in setup, every new Pinia instance rehydrates, and that's what the unit tests use to
  simulate a refresh.
- Getters: `hasDraft` (`draft !== null`), `itemCount` (sum of `quantity` over lines, 0 for no draft).
- Actions: `startOrder()` always replaces the draft with `createEmptyDraft()`, so a new order never inherits
  old lines (the "start over" invariant). `cancelOrder()` sets `draft = null`.
- Persistence: `watch(draft, (d) => saveDraft(d), { deep: true, flush: 'sync' })`. A sync flush means
  every mutation, including #26's future line edits, is in storage before the next line of code runs. No
  manual "save" call is ever needed, and the tests don't need `nextTick`.
- #26/#27/#28 add actions (`addLine`, `updateLine`, `removeLine`, `setPromoCode`, `setTip`). The state shape
  and the persisted schema stay `version: 1`. Any future change to the shape bumps `version` (old drafts are
  then discarded, which is acceptable for a draft). That rule goes in a comment next to the constant.

**Routes and guard:**
- `src/router/routes.ts`. Both routes are children of `MainLayout`, so the header and smoke spec stay as
  they are:
  - `''` → `name: 'start'`, `StartPage.vue`
  - `'order'` → `name: 'order'`, `OrderPage.vue`, `meta: { building: true }`
  - It also declares `declare module 'vue-router' { interface RouteMeta { building?: boolean } }`. #26/#27
    routes (e.g. `/order/review`) just set `building: true`, and they get both the guard and the Cancel
    button.
- `src/router/kiosk-guard.ts`: a pure function, `kioskRedirect(to: Pick<RouteLocationNormalized, 'name' |
  'meta'>, hasDraft: boolean): RouteLocationRaw | true`, unit-tested (tests pass plain `{ name, meta }`
  objects).
  - `meta.building && !hasDraft` → `{ name: 'start' }`. A deep link or bookmark to `/order` without a
    draft never shows a half-built page.
  - `name === 'start' && hasDraft` → `{ name: 'order' }`. While an order is open, the start screen
    isn't reachable by the back button or "Go Home". **Cancel is the only way out**, which is the kiosk rule.
  - Otherwise `true` (the 404 page and future non-kiosk routes are untouched).
  - No redirect loop: each redirect target passes the guard by construction (covered by unit tests).
- `src/router/index.ts`: `defineRouter(({ store }) => …)` registers
  `Router.beforeEach((to) => kioskRedirect(to, useDraftOrderStore(store).hasDraft))`, the Quasar-documented
  way to use a store in the router.

**Pages and components:**
- `src/pages/StartPage.vue` (replaces the placeholder `IndexPage.vue`):
  - A centred welcome ("Welcome!" plus a one-line prompt) and one big `q-btn` "Start order" (`size="xl"`,
    `unelevated`, `no-caps`, at least 96 px tall, up to 480 px wide, full width on narrow screens).
  - Click → `startOrder()`, then `router.push({ name: 'order' })`.
- `src/pages/OrderPage.vue`, the builder shell #26 fills in:
  - A "Your order" heading.
  - The empty state "No pizzas in your order yet." or "{n} pizza(s) in your order" from `itemCount` (a
    count, not money). That line also lets the E2E test prove a restored draft really came from storage.
- `src/components/CancelOrderButton.vue`:
  - A header `q-btn` "Cancel order" (`flat`, `size="lg"`, `no-caps`, icon `close`, at least 48 px tall).
  - It opens the **Quasar Dialog plugin**: title "Cancel this order?", message "Everything in your order will
    be removed.", cancel button **"Keep my order"**, ok button **"Yes, cancel order"** (`color: 'negative'`).
    Distinct labels keep Playwright selectors unambiguous.
  - `onOk` → `cancelOrder()` **first**, then `router.push({ name: 'start' })`. The order matters: if the push
    ran first, the guard would bounce back to `/order`.
  - Dismissing (Esc, backdrop, "Keep my order") keeps the draft. The dialog is not `persistent`, because
    dismissing is the safe outcome.
- `src/layouts/MainLayout.vue`: adds `<script setup>` with `useRoute()`, and shows `<CancelOrderButton>` in
  the toolbar when `route.meta.building`. The header is fixed (`hHh`), so Cancel is "always available while
  building", including on future building routes. The title "Pizza Order Designer" stays (the smoke spec
  depends on it).
- `quasar.config.ts`: `framework.plugins: ['Dialog']`. That's the only config change.

**Alternatives considered:**
- `pinia-plugin-persistedstate`: a new dependency for about 30 lines of code, and it still wouldn't validate
  the shape on restore. Rejected (the brief prefers hand-rolled).
- Quasar's `SessionStorage` plugin: it adds its own type-prefix encoding and still needs shape validation and
  try/catch. Little gain.
- A `QDialog` component inline instead of the Dialog plugin: more template for the same result.
- A separate `OrderLayout` for building routes: duplicates the header. A `meta.building` flag is lighter.
- The start screen showing "Continue order / Start new" when a draft exists: more UI and tests than the ACs
  ask for. The guard redirect is simpler and keeps one exit (Cancel).
- `crypto.randomUUID()` for a draft id: not needed in #25. It's only available in secure contexts (fine on
  `localhost`, but undefined over plain-HTTP LAN IPs), which #26 has to keep in mind for `DraftLine.id`. See 1.7.

### 1.3 Changes, file by file
All paths are under `client/`.

| File | New / Modify | What changes |
|---|---|---|
| `src/models/draft-order.ts` | New | `DraftLine`, `TipMode`, `TipInput`, `DraftOrder` types and `createEmptyDraft()`. No prices. |
| `src/stores/draft-order-persistence.ts` | New | `DRAFT_STORAGE_KEY`, `DRAFT_SCHEMA_VERSION = 1` (with a "bump on shape change" comment), `parseStoredDraft`, `loadDraft`, `saveDraft`. Never throws. |
| `src/stores/draft-order-store.ts` | New | `useDraftOrderStore`, a setup store: `draft`, `hasDraft`, `itemCount`, `startOrder`, `cancelOrder`, and a sync deep watch → `saveDraft`. |
| `src/router/kiosk-guard.ts` | New | The pure `kioskRedirect(to, hasDraft)`. |
| `src/router/routes.ts` | Modify | `start` and `order` (`meta.building`) children of `MainLayout`. `RouteMeta` augmentation. The catch-all stays last. |
| `src/router/index.ts` | Modify | Takes `{ store }` and registers `beforeEach` with `kioskRedirect`. |
| `src/pages/StartPage.vue` | New | The welcome screen and the large Start order button. |
| `src/pages/IndexPage.vue` | Delete | Replaced by `StartPage.vue` (the D-025 placeholder). |
| `src/pages/OrderPage.vue` | New | The builder shell: heading, empty state / item count. |
| `src/components/CancelOrderButton.vue` | New | The header button, the confirmation dialog, cancel → start. |
| `src/layouts/MainLayout.vue` | Modify | `<script setup>` and `CancelOrderButton` on `meta.building` routes. Title unchanged. |
| `quasar.config.ts` | Modify | `framework.plugins: ['Dialog']`. |
| `test/vitest/__tests__/IndexPage.test.ts` | Delete | It tested the placeholder page. Page behaviour is now covered by Playwright (D-034). |
| `test/vitest/__tests__/draft-order-persistence.test.ts` | New | Unit tests for parse/load/save (see 1.5). |
| `test/vitest/__tests__/draft-order-store.test.ts` | New | Unit tests for the store and rehydration (see 1.5). |
| `test/vitest/__tests__/kiosk-guard.test.ts` | New | Unit tests for the redirect table (see 1.5). |
| `test/e2e/kiosk-start.spec.ts` | New | Playwright specs for the flow, refresh, guards, broken storage and tablet width (see 1.5). |
| `test/e2e/smoke.spec.ts` | Unchanged | Still valid: `/` has no draft in a fresh context, so the banner shows the title and Go Home lands on `/`. |

No server changes, no README run-instruction changes (setup is unchanged), and no new npm dependencies.

### 1.4 Steps, in order (each step leaves the build green and becomes roughly one commit)
1. **Draft-order model, persistence and store.** Add `src/models/draft-order.ts`,
   `draft-order-persistence.ts`, `draft-order-store.ts`, and their two Vitest files. Nothing imports the store
   yet, so the app is unchanged.
   Commit: "Add draft-order store with sessionStorage persistence", `Refs #25`.
2. **Start screen, order page and kiosk guard.** Add `kiosk-guard.ts` and its test, update `routes.ts` and
   `router/index.ts`, add `StartPage.vue` and `OrderPage.vue`, delete `IndexPage.vue` and
   `IndexPage.test.ts`. Add `kiosk-start.spec.ts` with the start, refresh and guard specs (E2E 1, 4–7).
   Commit: "Add kiosk start screen, order page and draft route guard", `Refs #25`.
3. **Cancel order with confirmation.** Add `Dialog` to `quasar.config.ts`, add `CancelOrderButton.vue`, and
   show it in `MainLayout.vue`. Add the cancel specs (E2E 2, 3).
   Commit: "Add Cancel order with confirmation to the order builder", `Refs #25`.
4. **Resilience and tablet checks.** Add the broken-storage and tablet-viewport specs (E2E 8–10). Fix
   anything they turn up. Commit: "Cover broken session storage and tablet width in kiosk E2E", `Refs #25`.

Run the checks below after **each** step (E2E from step 2 on). From `client/`:
- `npx prettier --check --end-of-line auto <files changed in the step>`
- `npx eslint -c ./eslint.config.js "./src*/**/*.{ts,js,cjs,mjs,vue}" test/e2e test/vitest`
  (`test/vitest` is added to the brief's command because this story adds unit tests there)
- `npm run typecheck` (vue-tsc. `.quasar/tsconfig.json` includes `test/` too)
- `npm test` (Vitest)
- `npm run test:e2e` (Playwright, Chromium. These specs don't need the API.)
- `npx quasar build` (once, after step 4)

Do **not** rely on `npm run lint:check`: it runs prettier without `--check` and never fails (a known
pre-existing bug, out of scope).

Rough effort: 60–75 min of developer time.

### 1.5 Tests
Unit tests (Vitest, `test/vitest/__tests__/`). Each test starts with `sessionStorage.clear()`, then
`setActivePinia(createPinia())`, and restores any stubs afterwards (`vi.unstubAllGlobals()` /
`vi.restoreAllMocks()`).

| # | Test | Level | Invariant or AC it proves |
|---|---|---|---|
| U1 | `parseStoredDraft` round-trips a valid envelope with lines, promo and tip, and returns an object with only the known fields (extra keys dropped) | unit | AC 3. Restored state has exactly the model's shape |
| U2 | `parseStoredDraft` → `null` for: `null`, invalid JSON, `version: 2`, missing `draft`, `lines` not an array, a line with a non-integer or string `quantity`, `toppingCodes` not a string array, `tip.mode: 'cash'`, numeric `tip.value` | unit | Corrupt or old data never reaches the store (fail-safe restore) |
| U3 | `loadDraft` with an unreadable stored value returns `null` **and removes the key** | unit | A bad entry is cleaned up, not re-read forever |
| U4 | `loadDraft`/`saveDraft` don't throw when `getItem`/`setItem`/`removeItem` throw, or when accessing `sessionStorage` itself throws (stubbed via `vi.stubGlobal` / `Object.defineProperty`) | unit | The app still works when storage is unavailable or full |
| U5 | `startOrder()` → `hasDraft`, an empty draft (`lines: []`, `promoCode: null`, `tip: null`), `itemCount === 0`, and storage holds `{ version: 1, draft }` | unit | AC 1/3. A started order is persisted immediately (sync watch) |
| U6 | `cancelOrder()` → `draft === null`, `hasDraft === false`, and the storage key is removed | unit | AC 2. Cancel really clears the draft, in memory and in storage |
| U7 | Rehydration: start, then mutate `draft.lines` directly (simulating #26), then create a new Pinia → the new store has the same draft, deep-equal | unit | AC 3. A refresh keeps the draft, including nested mutations (deep watch) |
| U8 | Start over: seed storage with a draft that has lines, a promo and a tip, then `startOrder()` → lines empty, promo and tip null | unit | A new order never inherits the previous customer's lines |
| U9 | `itemCount` sums quantities across lines (e.g. 2 + 3 = 5) | unit | The count shown on the order page |
| U10 | With storage throwing, `startOrder`/`cancelOrder` still change the in-memory state | unit | Graceful degradation: memory-only mode |
| G1 | `kioskRedirect`: building route without a draft → `{ name: 'start' }`; start with a draft → `{ name: 'order' }`; start without a draft → `true`; building route with a draft → `true`; a route with neither (404) → `true` for both `hasDraft` values | unit | No half-built page without a draft, Cancel is the only exit, 404 untouched |
| G2 | Each redirect target passes `kioskRedirect` under the same `hasDraft` | unit | No redirect loop |

End-to-end tests (Playwright, `test/e2e/kiosk-start.spec.ts`, Chromium, each test in a fresh context so
sessionStorage starts empty). Seeded drafts write the envelope with the literal key and `version: 1` in a small
helper in the spec. If the key or schema changes, these tests fail loudly, which is intended.

| # | Test | Level | Invariant or AC it proves |
|---|---|---|---|
| E1 | `/` shows "Welcome!" and a **Start order** button, with no "Cancel order" in the header. Clicking Start order → URL `/order`, "Your order" and "No pizzas in your order yet." visible, "Cancel order" in the header | E2E | AC 1. AC 2 ("available while building", and only then) |
| E2 | Cancel order → the dialog shows → **Keep my order** → still on `/order`, the dialog is closed, and a reload still lands on `/order` | E2E | AC 2. Confirmation is required, and declining keeps the draft |
| E3 | Cancel order → **Yes, cancel order** → URL `/`, Start order visible, `sessionStorage` key gone (`page.evaluate`). Start order again → `/order` with an empty order | E2E | AC 2: clears and returns to start. "Start over" gives a fresh draft |
| E4 | Start order, then `page.reload()` → still on `/order` (without a restored draft the guard would send you to `/`) | E2E | AC 3. A refresh keeps the draft |
| E5 | Start order, then write a draft with one line of `quantity: 2` into sessionStorage, then reload → "2 pizzas in your order" | E2E | AC 3. State really comes back from storage, not just the URL |
| E6 | `goto('/order')` with no draft → redirected to `/`, Start order visible | E2E | The guard: no builder without a draft |
| E7 | With an active draft, `goto('/')` → redirected to `/order` | E2E | The guard: the start screen is unreachable while an order is open |
| E8 | `addInitScript` seeds `'{not json'` under the key, `goto('/order')` → lands on `/`, Start order works | E2E | Corrupt storage never breaks the app |
| E9 | `addInitScript` makes the `window.sessionStorage` getter throw `SecurityError`, then Start order → `/order`, Cancel → confirm → `/` | E2E | The app works with storage unavailable (memory-only) |
| E10 | `test.describe` with `test.use({ viewport: { width: 768, height: 1024 }, hasTouch: true })`: Start order's bounding box is ≥ 48 px tall and fits in the viewport. On `/order`, Cancel order is visible and ≥ 48 px tall | E2E | Kiosk feel: large touch targets at tablet width |
| S1–S2 | The existing `smoke.spec.ts` (unchanged) still passes | E2E | The app shell and 404 → Go Home still work |

**Tablet viewport project: not added now.** A second Playwright project would double every spec (smoke
included) for little gain while the only screens are a button and a placeholder. E10 checks the one tablet
claim that matters today. #26 (the customizer, whose AC says "works at tablet/kiosk widths") is the right time
to add a `tablet` project: `devices['Desktop Chrome']` with a 768×1024 viewport and `hasTouch`, **not**
`devices['iPad …']`, which defaults to WebKit, and only Chromium is installed (D-034).

### 1.6 Acceptance criteria mapping
| AC (from the issue) | Step(s) | Test(s) |
|---|---|---|
| Welcome screen with a large **Start order** button | 2 (and 4 for size) | E1, E10 |
| **Cancel order** always available while building. It asks for confirmation, clears the draft and returns to the start screen | 3 (and 2 for the `meta.building` flag) | E1, E2, E3, E9, U6, G1 |
| Draft kept in a Pinia store. A refresh keeps the draft (session storage) | 1, 2 | U1–U5, U7, U8, U10, E4, E5, E8 |
| Optional (P2): idle timeout returns to the start screen | **Deferred** (cut list, 1.1) | none |

### 1.7 Risks, assumptions, open questions

**Proposed decisions** (the numbers are placeholders. The orchestrator assigns the real ones, since another
session may take D-037+):
- **D-037: Kiosk flow and route guard.**
  - `/` (start) and `/order` (building, `meta.building`). Building routes need an active draft, the start
    route redirects to `/order` while one exists, and Cancel order (Quasar Dialog plugin, in the fixed header)
    is the only way out.
  - *Alternatives:* a "Continue / Start new" choice on the start screen, or a separate building layout.
- **D-038: Draft-order store and persistence.**
  - The model is `{ lines: [{ id, sizeCode, toppingCodes, quantity }], promoCode, tip: { mode, value } }`:
    menu **codes** only, **no prices**, and the tip value as a decimal **string**.
  - It's persisted to **sessionStorage** in a hand-rolled versioned envelope (`version: 1`), shape-validated
    on restore. Fail-safe: unreadable data is discarded, and when storage is unavailable the app runs in
    memory.
  - *Alternatives:* `pinia-plugin-persistedstate` (new dependency, no validation), Quasar `SessionStorage`,
    `localStorage` (would outlive the kiosk session).
- **D-039: The idle timeout (#25 AC 4, P2) is deferred to the cut list.** The reason and the later design
  are in 1.1.
- **Playwright tablet project deferred to #26.** One tablet-viewport spec (E10) for now. This could be part of
  D-037 instead of its own entry.

**README assumptions (proposed text):**
- "Cancelling an order before it's sent only clears it in this browser tab. Nothing reaches the server.
  Refreshing the page keeps the order in progress, and closing the tab discards it."
- "'Start over' means cancelling and then starting a new, empty order. While an order is in progress the start
  screen can't be reached except through **Cancel order**."
- "The kiosk has no idle timeout (cut for time)."

**Risks:**
- **Pinia/Quasar store-in-router wiring.** `useDraftOrderStore(store)` inside `defineRouter` is Quasar's
  documented pattern. If `store` is undefined in this Quasar version, calling `useDraftOrderStore()` inside the
  `beforeEach` callback works too (Pinia is installed before the first navigation). The developer notes which
  one was used.
- **E9 (throwing `sessionStorage` getter)** could also break dev tooling (Vite client / Quasar internals) that
  probes storage. Fallback: make `Storage.prototype.getItem/setItem/removeItem` throw instead, and note the
  deviation. U4 covers the getter case at unit level either way.
- **Dialog animation timing in E2E.** Use web-first assertions (`toBeVisible` / `toBeHidden` on
  `getByRole('dialog')`), not fixed waits.
- **Future `DraftLine.id` generation (#26).** `crypto.randomUUID()` only exists in secure contexts
  (`localhost` and HTTPS). Over plain HTTP on a LAN IP it's undefined. #26 should use a fallback or a counter.
  Recorded here so #26's planner sees it. It doesn't affect #25.
- **Stale codes in a restored draft** (the menu changed between refresh and quote): not a #25 concern. The
  server rejects unknown codes (#10), and #26/#27 surface that.
- **Two sessions editing `DECISIONS.md`** (D-036): number collisions are resolved by the orchestrator.

**Dependencies on M0 answers:** none. #7 (limits) is deliberately not enforced in the store. #2/#3 (pricing)
don't apply, because the store holds no money. #1 (scope) is open, but the user picked #25 explicitly
(D-035), and the P2 part (AC 4) is deferred.

**Questions for the user (non-blocking, the plan's default in brackets):**
1. Defer the idle timeout (AC 4) to the cut list? [Yes, defer.]
2. Should Cancel order skip the confirmation when the order is still empty? [No. The AC says it asks for
   confirmation, so it always asks.]
3. Is it OK that, while an order is in progress, `/` (and the 404 page's "Go Home") redirects to the order,
   so Cancel is the only way back to the start screen? [Yes, that's the kiosk rule.]

---

## 2. Plan review  *(pizza-plan-reviewer)*

### Round 1
**Verdict:** REVISE

The design itself is sound and lean: no new dependencies, codes rather than prices, a fail-safe restore, a
pure guard that can be unit-tested, and a store shape that later stories only add actions to. Two things need
revising. The plan was written against an **out-of-date M0 status**, and the promo field's shape hasn't been
checked against the decision that now exists. The rest are small fixes the planner can fold in during the
same round.

**What I checked against the code and installed packages:**
- **Store and router wiring.** `.quasar/dev-spa/app.js` runs `app.use(store)` and then
  `createRouter({ store })`. The first navigation happens later, at `app.use(router)` in `client-entry.js`. So
  `store` is defined, and `@quasar/app-vite/types/store.d.ts` types it as `Pinia` (`feature-flags.d.ts` has
  `store: true`). The fallback in the plan's first risk isn't needed.
- **Versions.** Pinia 4.1.0 still has the `defineStore(id, setup)` overload. vue-router 5.5.1 still documents
  the `declare module 'vue-router' { interface RouteMeta }` augmentation. Quasar 2.35.5's `QDialog` renders
  `role="dialog"`.
- **Storage use.** Neither Vite's client nor Quasar touches `sessionStorage`. Quasar probes `localStorage` only
  lazily, inside try/catch. So the E9 risk is low.
- **Vitest.** It installs happy-dom globals as configurable getters on `globalThis` (`populateGlobal`), so
  `vi.spyOn(globalThis, 'sessionStorage', 'get')` works.
- **tsconfig.** `exactOptionalPropertyTypes` and `noUncheckedIndexedAccess` are on in `.quasar/tsconfig.json`,
  which also includes `test/`.

| # | Severity | Finding | Suggested change |
|---|---|---|---|
| 1 | Major | **The M0 status is out of date.** The plan (1.1, 1.7 "Dependencies on M0 answers: none", the deferral argument) assumes #1–#7 are unanswered. On GitHub, **#1–#7 were all closed on 2026-11-18 (UTC)** with "Decided" comments: #1 "Full kiosk vision is in scope. The start screen (#25) … move from P2 → P1". #3: server-only pricing. #7: quantity 1–20, 1–25 lines, no duplicate toppings, tip with at most 2 decimals. Two things follow. (a) The main reason for deferring AC 4 ("#1 is still unanswered", "graders don't evaluate it") is weaker now that the kiosk vision is explicitly in scope. Without an idle timeout, the guard that keeps `/` unreachable while a draft exists means a walk-away customer's order is waiting for the next person. That's exactly the case the timeout covers. (b) "#7 unanswered; the store deliberately enforces none" needs restating. Not enforcing limits in the store is still right (the server validates, #26's UI caps), but now the reason is that the limits are a decided server rule. | Rebase 1.1 and 1.7 on the decided answers, and cite them. Keep recommending deferral if you still think it's right, but put the walk-away trade-off to the user (question 1), now that #25 is P1 under the kiosk vision. Cite #7's limits as the reason the store doesn't validate them. |
| 2 | Major | **The promo field's shape isn't checked against the decision.** `promoCode: string \| null` follows #2's *recommendation* (Q7, "One code field"). But #2's "Decided" comment doesn't mention how many codes an order can have. #7's decision says "SAVE5 + ILOVEPIZZA is rejected", and #11 and #28 both require a "cannot combine" message. With one field, a request can never carry two codes, so either that path can't be reached from the UI or #28 has to change the persisted shape. The brief requires the store to be forward-compatible with #28. | Check the accepted decision text for #2/#7 (ask the orchestrator; it lives in the other session's `DECISIONS.md`). If it's one code per order, keep `promoCode`, cite the decision in 1.2, and add a README assumption ("one promo code per order; entering a second replaces the first"). If an order can hold several codes, use `promoCodes: string[]` now. It also covers the one-code UI (a list of length ≤ 1). |
| 3 | Minor | **Step 2 wouldn't stay green.** E1 (added in step 2) checks that "Cancel order" appears in the header after Start order. `CancelOrderButton` only arrives in step 3. | Move E1's Cancel assertions to step 3, or add E1 in step 3. |
| 4 | Minor | **The E2E selectors aren't unambiguous.** Playwright's `name` matching is a case-insensitive *substring* by default, so `getByRole('button', { name: 'Cancel order' })` also matches "Yes, **cancel order**". While the dialog is open, or during its fade-out, that locator matches two buttons, and strict mode fails the test. That makes the 1.2 claim "distinct labels keep Playwright selectors unambiguous" untrue. | Scope the locators: `page.getByRole('banner').getByRole('button', { name: 'Cancel order' })` and `page.getByRole('dialog').getByRole('button', { name: 'Yes, cancel order' })`, or pass `exact: true`. Fix the sentence in 1.2. |
| 5 | Minor | **Decision numbering collides between the two sessions.** The other session's M0 decisions use **D-034 to D-040** (kiosk scope, rounding, server pricing, cancel = DELETE, simulated payment, kitchen advance, limits; quoted in #1–#7, #29, #31 and #33). This branch already uses D-034 to D-036 (Playwright, UI order, worktrees), and the plan's header and 1.7 cite D-034/D-035 and propose D-037 to D-039. | The planner should cite decisions by number **and title** (e.g. "D-034 (Playwright E2E)") so they survive renumbering. The orchestrator and the user have to settle the renumbering itself (see the report). |
| 6 | Minor | **`TipInput.value` sets the wire contract for #12/#14.** The draft is right to keep the raw string, since it holds exactly what the customer typed. But "#27's API client … sends it as a decimal string" decides #12's request format from a UI story. System.Text.Json doesn't read a `decimal` from a JSON string unless the DTO or the options allow it. | Keep the string in the draft. Reword it as "the wire format is decided in #12/#14; the draft keeps the raw input so no float is involved", and leave the contract to those stories. |
| 7 | Minor | **The fail-safe save is noisy.** With a deep `flush: 'sync'` watch, every mutation calls `saveDraft`. With storage unavailable, that's one `console.warn` per keystroke or edit once #26 lands. | Warn once per page load (a module-level flag). In U4, U10 and E9, spy on `console.warn` and assert it was called once, which also keeps the test output clean. |
| 8 | Minor | **Some details will fail lint or are fragile in the tests.** (a) `recommendedTypeChecked` is on, so `JSON.parse` returning `any` triggers `no-unsafe-*`, and a bare `router.push(...)` in a click or `onOk` handler triggers `no-floating-promises`. (b) U4's "`vi.stubGlobal` / `Object.defineProperty`" approach: `stubGlobal` can't make a getter throw, and a raw `defineProperty` isn't undone by `unstubAllGlobals`/`restoreAllMocks`. (c) Keep `useDraftOrderStore(store)` **inside** the `beforeEach` callback, as written. If it's hoisted to `defineRouter`'s top level, the store is created before `app.js` registers `store.use(({ store }) => { store.router = router })`, so that store would lack `router`. | (a) Type the parse result as `unknown` (`const parsed: unknown = JSON.parse(raw)`) and narrow it. Use `void router.push(…)` or `await` it. (b) Use `vi.spyOn(globalThis, 'sessionStorage', 'get')`, either throwing or returning a fake `Storage` whose methods throw. That's restored by `vi.restoreAllMocks()` and avoids happy-dom's internals. (c) Add a one-line note to the router step. Drop the "store undefined" fallback from the risks, since it's verified. |
| 9 | Minor | **Proportion.** 12 unit tests plus 10 E2E specs for a P1 start screen. E9 repeats what U4 and U10 already prove, using the riskiest technique (redefining a window getter in the browser), and E4 is a subset of E5. | Merge E4 into E5. Make E9 optional: keep it only if it passes first time, otherwise rely on U4/U10 and note that in the implementation notes. |
| 10 | Minor | **A cheap kiosk invariant is missing.** After Cancel, the browser Back button goes to `/order` with no draft. The guard handles it, but nothing proves it. Esc and backdrop dismissal are also untested (E2 only uses "Keep my order"). | In E3, after confirming, call `page.goBack()` and expect `/` and Start order. In E2, optionally also press `Escape` and expect the draft kept. |
| 11 | Minor | **The README edits have no owner.** 1.7 proposes three README assumptions, but README.md isn't in the 1.3 file list, and 1.3 says there are no README changes. | Either add README.md (Assumptions section) to step 4 of the file table, or state that the orchestrator adds the assumptions when the decisions are recorded. |

**Planner's own open points.** These are sound, apart from the AC 4 framing (finding 1).
- "Start over" as cancel + start a new order: sound, since no AC asks for a separate button.
- Cancel always confirms, even for an empty draft: sound, because the AC asks for confirmation.
- `/` redirects to `/order` while a draft exists: sound kiosk rule, but see the walk-away point in finding 1.
- One 768×1024 touch-target test instead of a tablet project (Chromium, not `devices['iPad …']`): sound.
- Deleting `IndexPage.vue` and its Vitest test: sound (it's the D-025 placeholder; UI behaviour is tested with
  Playwright).
- D-0xx placeholders: see finding 5.

---

## 3. Implementation notes  *(pizza-developer)*

### Round 1
- **Commits:** `<sha> <subject>`, …
- **Deviations from the plan, and why:** …
- **Checks run:** command → result
- **Follow-ups / proposed decisions:** …

---

## 4. Code review  *(pizza-reviewer)*

### Round 1
**Verdict:** PASS | CHANGES REQUESTED

**Traceability**
| AC | Implemented in | Proven by | Status |
|---|---|---|---|

**Plan conformance:** …

**Checks re-run:** command → result

| # | Severity | File:line | Finding and failure scenario | Suggested fix |
|---|---|---|---|---|

---

## 5. Outcome  *(orchestrator)*
- PR: …
- Decisions added: D-…
- Merged: <date>
