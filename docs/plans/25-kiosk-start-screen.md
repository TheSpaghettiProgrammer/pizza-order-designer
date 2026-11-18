# #25 — Kiosk start screen: begin, cancel and start over

| | |
|---|---|
| **Story** | https://github.com/TheSpaghettiProgrammer/pizza-order-designer/issues/25 |
| **Branch** | `story/25-kiosk-start-screen` (#42 and #43 merged into `main`; `main` merged into this branch) |
| **Status** | **Approved by the user (Gate 1, 2026-11-17 21:13)**, as written. In development. Plan review round 2 = APPROVE; 4 Minors for the developer to fold in. D-044/D-045 recorded |
| **Depends on** | #41 (Playwright harness, merged). **Follow-up:** #44 (idle timeout) builds on this story |
| **Decisions** | D-011 (same-origin `/api`), D-020 (Quasar CLI), D-024 (history router), D-025 (strip demo content), D-034 (Playwright E2E), D-035 (UI work order), D-037 (full kiosk scope), D-043 (validation limits, one promo code) |

**The user's answers (2026-11-17), for round 2:**
- **AC 4, the idle timeout:** moved to its own story, **#44** (P1, depends on #25). It's out of scope for #25. Keep the
  Cancel path reusable so #44 can call the same `cancelOrder()` → start-screen flow.
- **Cancel order always confirms**, even for an empty order: **kept**.
- **While an order is open, `/` (and the 404 page's "Go Home") redirect to `/order`**, so Cancel is the only exit: **kept**.
- **Promo field (reviewer Major 2):** one code per order (D-043), so `promoCode: string | null` stays.
- **Decision numbers:** D-034 to D-036 are the Playwright session's, and D-037 to D-043 are the M0 answers. New decisions
  from this plan get the next free numbers, **D-044 and up**, which the orchestrator assigns.

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

### Round 2

Round 2 is a complete plan. The developer follows **this round only**; Round 1 above is kept for the history.
Decisions are cited by number **and** title, so they survive renumbering.

#### Response to review round 1

| # | Finding | How Round 2 resolves it |
|---|---|---|
| 1 | Major: the M0 status is out of date (AC 4 deferral, #7 limits) | Rebased on the merged decisions. AC 4 is **out of #25** by the user's choice and is now **#44** (idle timeout, depends on #25). The walk-away case is #44's job, so the guard rule ("Cancel is the only exit", kept by the user) stands. For #44 the plan adds one shared exit path, `useEndOrder().endOrder()` (clear the draft, then go to start), used by the Cancel button now and by #44's countdown later (1.2, 1.7). The store not enforcing limits is now justified by D-043 (validation limits, one promo code): quantity 1–20, 1–25 lines and the topping set are server rules, and #26's UI caps them. Pricing: D-039 (server-only pricing), so the draft holds no money. |
| 2 | Major: the promo field's shape vs the decision | Resolved by D-043 (validation limits, one promo code): one code per order, so `promoCode: string \| null` stays. A README assumption line is proposed ("one promo code per order; entering a new one replaces the old one"). |
| 3 | Minor: step 2 wouldn't stay green (E1 checks Cancel before it exists) | E1 in step 2 checks only the start → order flow. The "Cancel only while building" assertions are added to E1 in step 3, together with `CancelOrderButton`. Each step's file list and tests are checked against what exists at that point (1.4). |
| 4 | Minor: ambiguous Playwright locators | All locators are scoped and exact: the Cancel button through `getByRole('banner')`, the dialog buttons through `getByRole('dialog')`, and `exact: true` everywhere (1.5, "Locators"). The Round 1 sentence about distinct labels is replaced. |
| 5 | Minor: decision-number collisions | Settled by the orchestrator (D-034 to D-043 are now fixed). This round cites number + title, and proposes **D-044 and D-045** as placeholders the orchestrator assigns. |
| 6 | Minor: `TipInput.value` sets the wire contract | Reworded. The draft keeps the customer's raw input as a string so no float touches money. The request's wire format is decided by #12/#14, and #27's client maps to it (1.2). |
| 7 | Minor: one `console.warn` per mutation with broken storage | Warn once. `createDraftPersistence()` returns `{ load, save }` with its own `warned` flag. The store creates one per instance, so it's once per page load, and tests get a fresh flag without a test-only export. U4, U10 (and the optional E8) assert exactly one warning. |
| 8a | Minor: lint (`no-unsafe-*`, `no-floating-promises`) | `const parsed: unknown = JSON.parse(raw)` narrowed by type guards. Every navigation is `await`ed or `void`ed (`void endOrder()`, `void router.push(...)`). |
| 8b | Minor: fragile storage stubbing in U4 | `vi.spyOn(globalThis, 'sessionStorage', 'get')`, either throwing a `DOMException('denied', 'SecurityError')` or returning a fake `Storage` whose methods throw. Undone by `vi.restoreAllMocks()`. |
| 8c | Minor: keep `useDraftOrderStore(store)` inside `beforeEach` | Stated in 1.2 and step 2 with the reason. The "store undefined" fallback is removed from the risks (verified by the reviewer). |
| 9 | Minor: proportion | Round 1's E4 is merged into the refresh spec (E4 now). The storage-unavailable E2E (E8) is **optional**: keep it only if it passes first time, otherwise rely on U4/U10 and note it. U9 (item count) is folded into U7, and U9 now checks that a fresh or corrupt session starts with no draft. Result: 12 unit tests (several are small `it.each` tables) + 8 required E2E specs (+1 optional), down from 12 + 10. |
| 10 | Minor: no Back-button / Escape checks | E3 calls `page.goBack()` after confirming and expects `/` with Start order. E2 also dismisses with `Escape` and expects the draft kept. |
| 11 | Minor: README has no owner | The **orchestrator** adds the README assumption lines (1.7) when it records D-044/D-045, alongside the M0 lines that #37 collects (D-037 says each decision becomes a README line). The developer doesn't touch `README.md`. |

#### 1.1 Goal and scope (Round 2)
#25 gives the kiosk its first real flow: a welcome screen with a large **Start order** button, then an order
builder at `/order` that holds a **draft order** in a Pinia store. A **Cancel order** button in the header always
asks for confirmation, clears the draft and returns to the start screen. The draft is saved to `sessionStorage`,
so a refresh keeps it. The store's types are designed now so #26 (line items), #27 (quote) and #28 (promo code,
tip) only **add actions**. The store holds **no prices** and does no money math, per D-039 (server-only
pricing). No backend change.

- **In scope:**
  - AC 1: welcome screen + large Start order button.
  - AC 2: Cancel order, available on every building route and only there. It always confirms (even for an
    empty order, the user's choice), clears the draft, and returns to the start screen.
  - AC 3: draft in Pinia, kept across a refresh through sessionStorage, and fail-safe when storage is corrupt,
    unavailable or full.
- **"Start over"** (the story title) means *cancel, then Start order again gives a fresh, empty draft*. No
  separate button, since no AC asks for one. README assumption (1.7).
- **Out of #25: AC 4, the idle timeout.** It's now story **#44** (P1, depends on #25), by the user's choice.
  #25 prepares for it in two ways, at no extra cost:
  - `meta.building` marks the routes where #44's timer runs.
  - `useEndOrder().endOrder()` is the single "clear the draft → start screen" path. The Cancel button uses it
    now, and #44's countdown calls the same function, so there's no duplicated logic (#44's AC).
- **Out of scope:** line items, menu, sizes and toppings (#26); totals and the quote (#27); tip and promo inputs
  (#28); cancelling a *submitted* order (#19, D-040 (cancel deletes the order)); validation limits. The store
  deliberately enforces none: D-043 (validation limits, one promo code) makes them server rules, and #26's UI
  caps the inputs.

#### 1.2 Approach (Round 2)

**Draft-order model** (`src/models/draft-order.ts`, types plus one factory, no Vue):
```ts
export interface DraftLine {
  id: string;             // client-side key for edit/remove and v-for; #26 generates it
  sizeCode: string;       // menu code from #8, e.g. 'large' (codes, never DB ids or prices)
  toppingCodes: string[]; // menu codes; a set per D-043, which #26's UI and the server enforce
  quantity: number;       // integer count; the 1–20 limit is a server rule (D-043)
}
export type TipMode = 'amount' | 'percent';
export interface TipInput {
  mode: TipMode;
  value: string;          // exactly what the customer typed, e.g. '2.50' or '15'
}
export interface DraftOrder {
  lines: DraftLine[];
  promoCode: string | null; // one code per order (D-043); raw input, the server trims and normalizes it
  tip: TipInput | null;     // null = no tip chosen yet
}
export function createEmptyDraft(): DraftOrder; // { lines: [], promoCode: null, tip: null }
```
- Required fields with `| null` rather than optional properties: `exactOptionalPropertyTypes` is on, and a fixed
  shape keeps the restore validation simple.
- **The tip stays a string** so no float ever touches money in the client. The **wire format** of the quote
  request (how a tip value is sent) is decided by #12/#14. #27's API client maps the draft to it. #25 decides
  nothing about the request.
- No money fields. The only number is `quantity`, a count. `itemCount` is a sum of counts.

**Persistence** (`src/stores/draft-order-persistence.ts`, hand-rolled, no new dependency):
```ts
export const DRAFT_STORAGE_KEY = 'pizza-order-designer:draft-order';
// Bump when the DraftOrder shape changes. Older drafts are then discarded on load (fine for a draft).
export const DRAFT_SCHEMA_VERSION = 1;
export function parseStoredDraft(raw: string | null): DraftOrder | null; // pure, never throws
export interface DraftPersistence {
  load(): DraftOrder | null;
  save(draft: DraftOrder | null): void;
}
export function createDraftPersistence(): DraftPersistence; // owns the warn-once flag
```
- The stored value is an envelope `{ "version": 1, "draft": DraftOrder }`.
- `parseStoredDraft`:
  - `null` → `null`. Otherwise `let parsed: unknown;` inside `try { parsed = JSON.parse(raw) } catch { return null }`,
    then narrowed with small type guards (`isRecord(v): v is Record<string, unknown>`, `isStringArray`, …).
    No `any`, so `no-unsafe-*` stays quiet.
  - Accepts only: `version === 1`; `lines` an array of `{ id: string, sizeCode: string, toppingCodes: string[],
    quantity: number }` with `Number.isInteger(quantity)`; `promoCode` a string or `null`; `tip` `null` or
    `{ mode: 'amount' | 'percent', value: string }`.
  - Returns a **fresh object with only the known fields**, so stray keys never reach the store.
  - It checks **shape only**. Whether a code is still on the menu, or a quantity is within 1–20, is the server's
    call (D-043; #10 returns field errors) and surfaces in #26/#27.
- `createDraftPersistence()` keeps `let warned = false` in its closure, plus `warnOnce(error)`, which logs one
  `console.warn("The order can't be saved in this browser, so it won't survive a refresh.", error)` and then
  stays silent.
  - `load()`: reads `globalThis.sessionStorage` **inside** the try (the property access itself can throw a
    `SecurityError`), then `getItem`. A thrown error → `warnOnce`, return `null`. If the raw value isn't `null`
    but doesn't parse, it calls `removeItem` (best-effort, errors → `warnOnce`) and returns `null`. Never throws.
  - `save(draft)`: `setItem(KEY, JSON.stringify({ version: 1, draft }))`, or `removeItem(KEY)` for `null`. Any
    error (quota, unavailable) → `warnOnce`. The app carries on in memory; only refresh survival is lost.
  - One persistence instance per store instance, so it warns **once per page load**, and each unit test gets a
    fresh flag without a test-only export.
- **sessionStorage, not localStorage:** a kiosk is shared. Closing the tab must not leave the last customer's
  order behind, while a refresh keeps it (what the AC asks for).

**Store** (`src/stores/draft-order-store.ts`, a Pinia **setup** store, `defineStore('draftOrder', () => …)`
imported from `pinia`):
```ts
const persistence = createDraftPersistence();
const draft = ref<DraftOrder | null>(persistence.load());       // null = no order in progress
const hasDraft = computed(() => draft.value !== null);
const itemCount = computed(() => draft.value?.lines.reduce((n, l) => n + l.quantity, 0) ?? 0);
function startOrder(): void { draft.value = createEmptyDraft(); }  // always fresh: the "start over" invariant
function cancelOrder(): void { draft.value = null; }
watch(draft, (d) => persistence.save(d), { deep: true, flush: 'sync' });
return { draft, hasDraft, itemCount, startOrder, cancelOrder };
```
- Loading in setup means every new Pinia instance rehydrates. The unit tests use that to simulate a refresh.
- The sync, deep watch means every mutation, including #26's future line edits, is in storage before the next
  statement runs. No manual save call, and the tests need no `nextTick`.
- #26/#27/#28 add actions (`addLine`, `updateLine`, `removeLine`, `setPromoCode`, `setTip`). The shape and the
  persisted schema stay `version: 1`.

**The shared exit path** (`src/composables/use-end-order.ts`):
```ts
export function useEndOrder() {
  const store = useDraftOrderStore();
  const router = useRouter();
  // Clear the draft FIRST, then navigate: while a draft exists the guard sends `/` back to `/order`.
  // Used by Cancel order (#25) and by the idle-timeout countdown (#44).
  async function endOrder(): Promise<void> {
    store.cancelOrder();
    await router.push({ name: 'start' });
  }
  return { endOrder };
}
```
- Called in component setup (it uses `useRouter()`).
- For #44: QDialog hides itself on a route change unless it's `persistent` (Quasar's `hideOnRouteChange`, checked
  in `quasar/src/components/dialog/QDialog.js`), and the Dialog plugin shares the app's context, so the router
  is visible to it. So if #44's countdown calls `endOrder()` while the Cancel dialog is open, the dialog should
  close as the route changes. #44's spec should still prove it.

**Routes and guard:**
- `src/router/routes.ts`: both routes are children of `MainLayout`, so the header and the smoke spec stay valid.
  - `''` → `name: 'start'`, `StartPage.vue`.
  - `'order'` → `name: 'order'`, `OrderPage.vue`, `meta: { building: true }`.
  - `declare module 'vue-router' { interface RouteMeta { building?: boolean } }`. Later building routes (e.g.
    `/order/review`) only set `building: true` to get the guard, the Cancel button and #44's timer.
  - The catch-all 404 route stays last and outside `MainLayout`, unchanged.
- `src/router/kiosk-guard.ts`, a pure function:
  `kioskRedirect(to: Pick<RouteLocationNormalized, 'name' | 'meta'>, hasDraft: boolean): RouteLocationRaw | true`.
  - `to.meta.building && !hasDraft` → `{ name: 'start' }`. A deep link or bookmark to `/order` without a draft
    never shows a half-built page, and Back after a cancel lands on start.
  - `to.name === 'start' && hasDraft` → `{ name: 'order' }`. While an order is open, `/`, the Back button and
    the 404 page's "Go Home" all lead to the order, so **Cancel is the only exit** (the user's choice).
  - Otherwise `true` (the 404 page and future non-kiosk routes are untouched).
  - No loop: each redirect target passes the guard under the same `hasDraft` (G2).
- `src/router/index.ts`: `defineRouter(({ store }) => { … Router.beforeEach((to) => kioskRedirect(to,
  useDraftOrderStore(store).hasDraft)); … })`.
  - `useDraftOrderStore(store)` stays **inside** the `beforeEach` callback. Hoisted to `defineRouter`'s top
    level, the store would be created before Quasar's `app.js` registers the plugin that sets `store.router`.
  - Quasar installs Pinia before it creates the router, and the first navigation happens later, so the store
    (and its rehydration) is ready for the first guard call.

**Pages and components:**
- `src/pages/StartPage.vue` (replaces the placeholder `IndexPage.vue`):
  - Centred `<h1 class="text-h3">Welcome!</h1>`, a one-line prompt, and one big `q-btn` "Start order"
    (`size="xl"`, `unelevated`, `no-caps`, `color="primary"`, at least 96 px tall, up to 480 px wide, full width
    on narrow screens).
  - Click → `store.startOrder()`, then `void router.push({ name: 'order' })`.
- `src/pages/OrderPage.vue`, the builder shell #26 fills in:
  - `<h1 class="text-h4">Your order</h1>`.
  - "No pizzas in your order yet." when `itemCount === 0`, otherwise "1 pizza in your order" /
    "{n} pizzas in your order". A count, not money. It also lets the E2E prove the draft came back from storage.
- `src/components/CancelOrderButton.vue`:
  - A header `q-btn` "Cancel order" (`flat`, `size="lg"`, `no-caps`, icon `close`, at least 48 px tall). QIcon's
    ligature is `aria-hidden`, so the button's accessible name is just "Cancel order".
  - Click → `$q.dialog({ title: 'Cancel this order?', message: 'Everything in your order will be removed.',
    cancel: { label: 'Keep my order', flat: true, noCaps: true }, ok: { label: 'Yes, cancel order', color:
    'negative', noCaps: true }, focus: 'cancel' })`. `focus: 'cancel'` means a stray Enter keeps the order.
  - `.onOk(() => { void endOrder(); })`. Dismissing (Escape, backdrop, "Keep my order") keeps the draft. Not
    `persistent`, because dismissing is the safe outcome.
- `src/layouts/MainLayout.vue`: adds `<script setup lang="ts">` with `useRoute()`, and shows
  `<CancelOrderButton v-if="route.meta.building" />` in the toolbar after the title. The header is fixed (`hHh`),
  so Cancel is available throughout building. The title "Pizza Order Designer" stays (the smoke spec checks it).
- `quasar.config.ts`: `framework.plugins: ['Dialog']`. The only config change.

**Alternatives considered** (unchanged from Round 1, plus one):
- `pinia-plugin-persistedstate`: a new dependency for ~30 lines, and no shape validation on restore. Rejected.
- Quasar's `SessionStorage` plugin: its own type-prefix encoding, and still needs validation and try/catch.
- An inline `QDialog` instead of the Dialog plugin: more template for the same result.
- A separate `OrderLayout` for building routes: duplicates the header. `meta.building` is lighter.
- "Continue order / Start new" on the start screen: more UI than the ACs ask for; the guard keeps one exit.
- **New:** navigating inside the store's `cancelOrder()` (or a watcher that leaves building routes when the
  draft disappears). It would also give #44 one path, but it hides navigation in a state action or a side
  effect. An explicit composable is easier to read and test.

#### 1.3 Changes, file by file (Round 2)
All paths are under `client/`.

| File | New / Modify | What changes | Step |
|---|---|---|---|
| `src/models/draft-order.ts` | New | `DraftLine`, `TipMode`, `TipInput`, `DraftOrder`, `createEmptyDraft()`. No prices. | 1 |
| `src/stores/draft-order-persistence.ts` | New | `DRAFT_STORAGE_KEY`, `DRAFT_SCHEMA_VERSION = 1` (bump comment), `parseStoredDraft`, `createDraftPersistence` (warn once). Never throws. | 1 |
| `src/stores/draft-order-store.ts` | New | `useDraftOrderStore`: `draft`, `hasDraft`, `itemCount`, `startOrder`, `cancelOrder`, sync deep watch → `save`. | 1 |
| `test/vitest/__tests__/draft-order-persistence.test.ts` | New | U1–U4 | 1 |
| `test/vitest/__tests__/draft-order-store.test.ts` | New | U5–U10 | 1 |
| `src/router/kiosk-guard.ts` | New | Pure `kioskRedirect(to, hasDraft)`. | 2 |
| `test/vitest/__tests__/kiosk-guard.test.ts` | New | G1, G2 | 2 |
| `src/router/routes.ts` | Modify | `start` and `order` (`meta.building`) children of `MainLayout`, `RouteMeta` augmentation, catch-all last. | 2 |
| `src/router/index.ts` | Modify | Takes `{ store }`; `beforeEach` → `kioskRedirect` (store looked up inside the callback). | 2 |
| `src/pages/StartPage.vue` | New | Welcome heading and the large Start order button. | 2 |
| `src/pages/OrderPage.vue` | New | "Your order" heading and the empty state / item count. | 2 |
| `src/pages/IndexPage.vue` | Delete | The D-025 (strip demo content) placeholder, replaced by `StartPage.vue`. | 2 |
| `test/vitest/__tests__/IndexPage.test.ts` | Delete | Tested the placeholder. Page behaviour is tested with Playwright (D-034, Playwright E2E). | 2 |
| `test/e2e/kiosk-start.spec.ts` | New | Step 2: helpers, E1 (flow part), E4–E7. Step 3: E1 banner part, E2, E3. Step 4: E8 (optional), E9. | 2–4 |
| `quasar.config.ts` | Modify | `framework.plugins: ['Dialog']`. | 3 |
| `src/composables/use-end-order.ts` | New | `useEndOrder().endOrder()`: clear the draft, then push `start`. Shared with #44. | 3 |
| `src/components/CancelOrderButton.vue` | New | Header button + confirmation dialog → `endOrder()`. | 3 |
| `src/layouts/MainLayout.vue` | Modify | `<script setup>`, `CancelOrderButton` on `meta.building` routes. Title unchanged. | 3 |
| `test/e2e/smoke.spec.ts` | Unchanged | Still valid: a fresh context has no draft, so `/` shows the banner title and Go Home lands on `/`. | – |

No server changes, no new npm dependencies, no `README.md` edits (the orchestrator adds the assumption lines,
1.7), no Playwright config changes.

#### 1.4 Steps, in order (Round 2; each step leaves lint, types, unit and E2E green, ~one commit each)
1. **Draft-order model, persistence and store** (+ U1–U10). Nothing imports the store yet, so the app and the
   existing `IndexPage.test.ts` and smoke specs are unchanged and green.
   Commit: "Add draft-order store with sessionStorage persistence", `Refs #25`.
2. **Start screen, order page and kiosk guard** (+ G1, G2, and in `kiosk-start.spec.ts`: E1 *without* any Cancel
   assertion, E4, E5, E6, E7). Delete `IndexPage.vue` and `IndexPage.test.ts` in this same step (the unit suite
   still has U1–U10, so Vitest doesn't run empty). Smoke stays green: a fresh context has no draft.
   After this step an open order can't be left from the UI yet (Cancel arrives in step 3). That's fine for an
   intermediate commit on the branch, and no spec in step 2 needs to leave an order.
   Commit: "Add kiosk start screen, order page and draft route guard", `Refs #25`.
3. **Cancel order with confirmation**: the Dialog plugin, `use-end-order.ts`, `CancelOrderButton.vue`,
   `MainLayout.vue` (+ E1's banner assertions, E2, E3).
   Commit: "Add Cancel order with confirmation to the order builder", `Refs #25`.
4. **Resilience and tablet checks** (+ E9, and E8 if it passes first time; otherwise drop it and say so in the
   implementation notes, since U4/U10 cover it). Then `npx quasar build`.
   Commit: "Cover tablet touch targets and unavailable storage in kiosk E2E", `Refs #25`.

**Checks after each step**, from `client/`:
- `npx prettier --check --end-of-line auto <files changed in the step>`
- `npx eslint -c ./eslint.config.js "./src*/**/*.{ts,js,cjs,mjs,vue}" test/e2e test/vitest`
- `npm run typecheck` (vue-tsc; `.quasar/tsconfig.json` includes `test/`)
- `npm test` (Vitest)
- `npm run test:e2e` (Playwright, Chromium, own dev server on :9100; these specs don't need the API)
- `npx quasar build` (once, after step 4)

Don't rely on `npm run lint:check` (it runs prettier without `--check`, plus a CRLF issue). It's being fixed on
a separate `fix/` branch. If that fix is merged into this branch before implementation, the developer may use it
as well, but the commands above stay the reference.

Rough effort: 55–70 min of developer time.

#### 1.5 Tests (Round 2)
**Unit tests** (Vitest, `test/vitest/__tests__/`). `beforeEach`: `sessionStorage.clear()`,
`setActivePinia(createPinia())`. `afterEach`: `vi.restoreAllMocks()`. Storage failures are simulated only with
`vi.spyOn(globalThis, 'sessionStorage', 'get')`, either `.mockImplementation(() => { throw new
DOMException('denied', 'SecurityError') })` or `.mockReturnValue(fakeStorage)`, where `fakeStorage`'s
`getItem`/`setItem`/`removeItem` throw. `console.warn` is spied (`vi.spyOn(console, 'warn').mockImplementation(()
=> {})`) wherever storage fails, which keeps the output clean and lets the test count warnings.

| # | Test | Level | Invariant or AC it proves |
|---|---|---|---|
| U1 | `parseStoredDraft` round-trips a valid envelope with lines, promo and tip, and the result has only the known fields (extra keys on the envelope, the draft and a line are dropped) | unit | AC 3. Restored state has exactly the model's shape |
| U2 | `it.each`: `parseStoredDraft` → `null` for `null`, invalid JSON, `version: 2`, missing `draft`, `lines` not an array, `quantity` `1.5` or `'2'`, `toppingCodes` not a string array, `tip.mode: 'cash'`, numeric `tip.value`, `promoCode: 5` | unit | Corrupt or old data never reaches the store |
| U3 | `load()` with an unparseable stored value returns `null` **and removes the key** | unit | A bad entry is cleaned up, not re-read on every load |
| U4 | `it.each` over {getter throws, methods throw}: `load()` returns `null`, `save(draft)` and `save(null)` don't throw, and `console.warn` was called **exactly once** across all three calls | unit | Unavailable or full storage never breaks the app, and warns once |
| U5 | `startOrder()` → `hasDraft`, draft deep-equals `{ lines: [], promoCode: null, tip: null }`, `itemCount === 0`, and storage holds `{ version: 1, draft }` | unit | AC 1/3. A started order is persisted immediately (sync watch) |
| U6 | `cancelOrder()` after a start → `draft === null`, `hasDraft === false`, and the key is removed | unit | AC 2. Cancel clears the draft in memory and in storage |
| U7 | Rehydration: start, push two lines directly onto `draft.lines` (quantities 2 and 3, simulating #26), then `setActivePinia(createPinia())` and a new store → same draft (deep-equal) and `itemCount === 5` | unit | AC 3. A refresh keeps the draft, including nested mutations (deep watch); the count sums quantities |
| U8 | Start over: seed storage with a draft that has lines, a promo and a tip, create the store (rehydrated), then `startOrder()` → lines empty, promo and tip `null`, and storage holds the empty draft | unit | A new order never inherits the previous customer's draft |
| U9 | No stored draft, or an unparseable one → a new store has `draft === null` and `hasDraft === false` | unit | A fresh or corrupt session starts at the start screen |
| U10 | With the getter throwing: a new store starts with `draft === null`; `startOrder()`, a line mutation and `cancelOrder()` all change the in-memory state; `console.warn` called **exactly once** in total | unit | Memory-only mode works, and stays quiet after the first warning |
| G1 | `it.each`, `kioskRedirect`: building route without a draft → `{ name: 'start' }`; start with a draft → `{ name: 'order' }`; start without a draft → `true`; building route with a draft → `true`; an unnamed route with empty meta (the 404) → `true` for both `hasDraft` values | unit | No half-built page without a draft; Cancel is the only exit; 404 untouched |
| G2 | For each redirect in G1, resolving the target to its route (`{ name: 'start', meta: {} }` / `{ name: 'order', meta: { building: true } }`) and calling `kioskRedirect` again with the same `hasDraft` → `true` | unit | No redirect loop |

**End-to-end tests** (Playwright, `test/e2e/kiosk-start.spec.ts`, Chromium; each test gets a fresh context, so
sessionStorage starts empty). Seeded drafts use a small helper in the spec with the **literal** key
`'pizza-order-designer:draft-order'` and `version: 1`, so a change to the key or schema fails these tests loudly
(intended).

*Locators* (defined once at the top of the spec; all exact, all scoped):
```ts
const startButton  = (p: Page) => p.getByRole('button', { name: 'Start order', exact: true });
const cancelButton = (p: Page) => p.getByRole('banner').getByRole('button', { name: 'Cancel order', exact: true });
const dialog       = (p: Page) => p.getByRole('dialog');
const keepButton   = (p: Page) => dialog(p).getByRole('button', { name: 'Keep my order', exact: true });
const confirmButton = (p: Page) => dialog(p).getByRole('button', { name: 'Yes, cancel order', exact: true });
```
Dialog open/close is asserted with web-first `toBeVisible()` / `toBeHidden()` on `dialog(page)`, never fixed waits.

| # | Test | Step | Invariant or AC it proves |
|---|---|---|---|
| E1 | `/` shows the "Welcome!" heading and Start order. Click → URL `/order`, "Your order" heading and "No pizzas in your order yet." visible. **Step 3 adds:** `cancelButton` is hidden on `/` and visible on `/order` | 2, 3 | AC 1. AC 2: Cancel is available while building, and only then |
| E2 | On `/order`: Cancel order → dialog visible → **Keep my order** → dialog hidden, still `/order`. Cancel order again → press `Escape` → dialog hidden, still `/order`. Reload → still `/order` | 3 | AC 2. Confirmation is required; declining (button or Escape) keeps the draft |
| E3 | On `/order`: Cancel order → **Yes, cancel order** → URL `/`, Start order visible, storage key gone (`page.evaluate`). `page.goBack()` → URL `/`, Start order visible (the guard sends the draft-less `/order` back to start). Start order again → `/order`, "No pizzas in your order yet." | 3 | AC 2: clears and returns to start; Back can't revive a cancelled order; "start over" gives a fresh draft. Also proves `endOrder()` clears **before** navigating (otherwise the guard would bounce to `/order`) |
| E4 | Start order, then seed a draft with one line of `quantity: 2`, reload → still `/order` and "2 pizzas in your order" | 2 | AC 3. A refresh keeps the draft, and the state really comes from storage, not just the URL (merges Round 1's E4 and E5) |
| E5 | `goto('/order')` with no draft → URL `/`, Start order visible | 2 | The guard: no builder without a draft |
| E6 | Start order, then `goto('/')` → URL `/order`. Then `goto('/no/such/page')` → 404 → **Go Home** → URL `/order` | 2 | The guard: while an order is open, the start screen is unreachable, including from the 404 page (the user's choice) |
| E7 | `addInitScript` puts `'{not json'` under the key; `goto('/order')` → URL `/`, storage key removed; Start order → `/order` | 2 | Corrupt storage never breaks the app and is cleaned up |
| E8 *(optional)* | `addInitScript` makes the `window.sessionStorage` getter throw a `SecurityError`; Start order → `/order`; Cancel → confirm → `/`; at most one `console` warning collected via `page.on('console')` | 4 | The app works with storage unavailable (memory-only). Dropped if it isn't green first time; U4/U10 cover it |
| E9 | `test.describe` with `test.use({ viewport: { width: 768, height: 1024 }, hasTouch: true })`: Start order's bounding box is ≥ 48 px tall and lies within the 768 px width. On `/order`, Cancel order is visible and ≥ 48 px tall | 4 | Kiosk feel: large touch targets at tablet width |
| S1–S2 | The existing `smoke.spec.ts`, unchanged | – | The app shell and the 404 → Go Home path still work |

**Tablet viewport project: not added now** (unchanged from Round 1). E9 covers the one tablet claim that matters
today. #26 (the customizer, whose AC says "works at tablet/kiosk widths") is the time to add a `tablet` project:
`devices['Desktop Chrome']` with a 768×1024 viewport and `hasTouch`, not `devices['iPad …']` (WebKit; only
Chromium is installed per D-034, Playwright E2E).

#### 1.6 Acceptance criteria mapping (Round 2)
| AC (from the issue) | Step(s) | Test(s) |
|---|---|---|
| Welcome screen with a large **Start order** button | 2 (4 for the size check) | E1, E9 |
| **Cancel order** always available while building. It asks for confirmation, clears the draft and returns to the start screen | 3 (2 for `meta.building` and the guard) | E1 (step 3 part), E2, E3, E9, U6, G1, G2 (E8 optional) |
| Draft kept in a Pinia store. A page refresh keeps the draft (session storage) | 1, 2 | U1–U5, U7–U10, E4, E5, E7 |
| Optional (P2): idle timeout returns to the start screen | **Out of #25: story #44** (the user's choice). #25 provides `meta.building` and `endOrder()` for it | none in #25 |

#### 1.7 Risks, assumptions, open questions (Round 2)

**Decisions this plan relies on:** D-011 (same-origin `/api`), D-020 (Quasar CLI), D-024 (history router), D-025
(strip demo content), D-034 (Playwright E2E), D-035 (UI work order), D-037 (full kiosk scope), D-039 (server-only
pricing), D-043 (validation limits, one promo code). None is contradicted.

**Proposed decisions** (placeholders; the orchestrator assigns the numbers):
- **D-044: Kiosk flow, route guard and the shared exit path.**
  - `/` (start) and `/order` (building, `meta.building`). Building routes need an active draft; the start route
    redirects to `/order` while one exists (so `/`, Back and the 404's "Go Home" lead back to the order), and
    **Cancel order** is the only exit. Cancel always confirms, even for an empty order (Quasar Dialog plugin,
    default focus on "Keep my order").
  - Leaving an order always goes through `useEndOrder().endOrder()` (clear the draft, then go to start). #44's
    idle timeout reuses it and runs only on `meta.building` routes.
  - Playwright tablet project deferred to #26; one 768×1024 touch-target spec for now.
  - *Alternatives:* "Continue / Start new" on the start screen; a separate building layout; navigating from
    inside the store.
- **D-045: Draft-order store and persistence.**
  - Model `{ lines: [{ id, sizeCode, toppingCodes, quantity }], promoCode: string | null, tip: { mode, value } |
    null }`: menu **codes** only, **no prices** (D-039, server-only pricing), one promo code (D-043), the tip
    kept as the customer's raw **string**. The quote request's wire format is left to #12/#14.
  - Persisted to **sessionStorage** in a hand-rolled versioned envelope (`version: 1`, bump on any shape
    change), shape-validated on restore. Fail-safe: unreadable data is discarded and removed; with storage
    unavailable the app runs in memory and warns once per page load. Limits (D-043) are not enforced in the store.
  - *Alternatives:* `pinia-plugin-persistedstate` (new dependency, no validation), Quasar `SessionStorage`,
    `localStorage` (would outlive the kiosk session).

**README assumptions** (proposed text; **the orchestrator** adds them when it records D-044/D-045; the developer
doesn't edit `README.md`):
- "Cancelling an order before it's sent only clears it in this browser tab; nothing reaches the server.
  Refreshing the page keeps the order in progress, and closing the tab discards it."
- "'Start over' means cancelling and then starting a new, empty order. While an order is in progress, the start
  screen can only be reached through **Cancel order**, which always asks for confirmation."
- "One promo code per order; entering a new one replaces the old one." (D-043; the UI part lands with #28.)

**Risks:**
- **E8 (throwing `sessionStorage` getter in the browser).** Low risk (the reviewer found that neither Vite's
  client nor Quasar touches `sessionStorage`), but it's optional: if it isn't green first time, drop it and note
  it. U4/U10 prove the same at unit level.
- **Dialog animation timing.** Web-first `toBeVisible` / `toBeHidden` only, no fixed waits. Scoped, exact
  locators mean the fading dialog's "Yes, cancel order" can never match the header's "Cancel order".
- **Button accessible names.** `exact: true` relies on QIcon's ligature being `aria-hidden` (checked in
  `QIcon.js`). If a name still includes icon text, the developer drops the icon from that button rather than
  loosening the locator, and notes it.
- **Intermediate state after step 2:** an open order can't be left from the UI until step 3. Branch-only, and no
  step 2 spec depends on leaving.
- **Future `DraftLine.id` generation (#26).** `crypto.randomUUID()` only exists in secure contexts (`localhost`,
  HTTPS), not over plain HTTP on a LAN IP. #26 should use a fallback or a counter. Recorded for #26's planner.
- **Stale codes in a restored draft** (the menu changed between refresh and quote): the server rejects unknown
  codes (D-043, #10), and #26/#27 surface that.
- *(Removed from Round 1: the "store undefined in `defineRouter`" fallback. The reviewer verified that `store` is
  defined and typed as `Pinia`.)*

**Questions for the user:** none open. The four Round 1 questions are answered in the header: idle timeout → #44;
Cancel always confirms; `/` and "Go Home" redirect to `/order` while an order is open; `promoCode: string | null`
stays (D-043).

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

### Round 2
**Verdict:** APPROVE

Round 2 resolves both Majors and all nine Minors. The design is the same lean one from Round 1, and it now
rests on the merged decisions and the user's answers. `useEndOrder().endOrder()` gives #44 the single exit
path that its AC asks for. Every step's specs only use what exists at that step. I found no new Blocker or
Major. The four new Minors below are small, and **the developer folds them in during implementation**. None
of them needs another planning round.

**What I checked this round, against the code and installed packages:**
- **Baseline lint.** On this branch, `npx eslint -c ./eslint.config.js "./src*/**/*.{ts,js,cjs,mjs,vue}" test/e2e test/vitest`
  exits 0, so adding `test/vitest` to the command is safe. `.quasar/tsconfig.json` includes `./../**/*`, so
  test files are type-checked and type-aware linted.
- **Dialog options.** Quasar 2.35.5's `QDialogOptions` has `focus?: "ok" | "cancel" | "none"`
  (`DialogPluginComponent.js` validates it). `cancel`/`ok` accept `QBtnProps` objects, so `noCaps`/`flat` type-check.
- **#44 reuse.** `use-model-toggle.js` watches `proxy.$route.fullPath` and hides the dialog when
  `hideOnRouteChange` is set and a router exists. That matches the plan's note that an `endOrder()` from #44
  closes an open Cancel dialog. Hiding it this way doesn't run its `onOk`, so the order can't end twice.
  `endOrder()` is exactly #44's "`cancelOrder()` → start screen" path, and it's callable from a
  `useIdleTimeout` composable in `MainLayout` setup.
- **Router types (vue-router 5.5.1).** It exports `RouteLocationNormalized`, `RouteLocationRaw`, `RouteMeta`
  and `NavigationGuardReturn`. `RouteRecordNameGeneric` is `string | symbol | undefined`, so the 404 case in G1
  (`name: undefined`) type-checks under `exactOptionalPropertyTypes`.
- **Vitest 5.1 / happy-dom 20.** `populateGlobal` defines each window key on `globalThis` as a configurable
  getter, so `vi.spyOn(globalThis, 'sessionStorage', 'get')` works and `restoreAllMocks()` undoes it.
- **Guard and history.** `to.meta` merges the matched records' meta, so the `order` child's `building: true`
  shows up under the unnamed `MainLayout` parent. On a Back (popstate) into `/order` with no draft, vue-router
  follows the guard's redirect, and E3's `goBack()` → `/` holds. On the 404 page, "Go Home" (`to="/"`) resolves
  to `name: 'start'`, so E6's redirect to `/order` holds. The smoke specs use fresh contexts, so they stay green.
- **Decision citations.** D-037, D-039, D-040 and D-043 match the titles and content in `docs/DECISIONS.md`
  (D-043: quantity 1–20, 1–25 lines, toppings a set, "one code per order").

**Round 1 findings:**

| # | Round 1 finding | Status |
|---|---|---|
| 1 | Major: out-of-date M0 status | **Resolved.** AC 4 moved to #44 by the user. The limits are justified by D-043 and pricing by D-039. The walk-away case now belongs to #44. |
| 2 | Major: promo shape vs the decision | **Resolved.** D-043 says one code per order, so `promoCode: string \| null` stays, and the README line is proposed. |
| 3 | Minor: step 2 not green | **Resolved.** E1's Cancel assertions move to step 3, and each spec is tagged with its step. |
| 4 | Minor: ambiguous locators | **Resolved.** Locators are scoped to the banner and the dialog, all with `exact: true`. |
| 5 | Minor: decision numbering | **Resolved.** Decisions are cited by number and title. D-044/D-045 are placeholders the orchestrator assigns. |
| 6 | Minor: tip wire contract | **Resolved.** The wire format is left to #12/#14. |
| 7 | Minor: noisy warnings | **Resolved.** Each `createDraftPersistence()` instance warns once, and U4/U10 assert exactly one warning. |
| 8a | Minor: `no-unsafe-*` / `no-floating-promises` | **Resolved** for `src/`. The same `unknown` rule should apply in the tests (new Minor 1). |
| 8b | Minor: fragile storage stubbing | **Resolved.** It uses `vi.spyOn(globalThis, 'sessionStorage', 'get')`. |
| 8c | Minor: store lookup inside `beforeEach` | **Resolved.** The plan states it with the reason, and the fallback is removed. |
| 9 | Minor: proportion | **Resolved.** The E2E list shrinks to 8 specs plus 1 optional, and the storage-unavailable E2E is optional. |
| 10 | Minor: Back / Escape untested | **Resolved.** E3 adds `goBack()` and E2 adds `Escape`. |
| 11 | Minor: README owner | **Resolved.** The orchestrator adds the README lines, and the developer doesn't touch `README.md`. |

**New findings (Round 2):** all Minor, for the developer to fold in during implementation.

| # | Severity | Finding | Suggested change |
|---|---|---|---|
| 1 | Minor | **Lint traps in the unit tests.** `DraftPersistence` declares `load()`/`save()` as *method* signatures. `recommendedTypeChecked` turns on `@typescript-eslint/unbound-method`, so a test that writes `const { load, save } = createDraftPersistence()` fails lint. Separately, tests that read storage back with `JSON.parse(sessionStorage.getItem(KEY)!)` and then access `.version`/`.draft` get `any`, which trips `no-unsafe-member-access`/`no-unsafe-assignment`. | In the tests, call the methods on the object (`const p = createDraftPersistence(); p.load()`), or declare the members as properties (`load: () => DraftOrder \| null`). Type parsed storage as `unknown` (`const stored: unknown = JSON.parse(…)`) and compare the whole value with `toEqual({ version: 1, draft: … })`. |
| 2 | Minor | **E9 checks less than the design promises.** 1.2 sizes Start order at "at least 96 px tall" to make it the AC's *large* button, but E9 only asserts ≥ 48 px, the generic touch-target minimum. A regression to an ordinary button would still pass. | Assert ≥ 96 px for Start order, the design value backing "large". Keep ≥ 48 px for Cancel order. |
| 3 | Minor | **E8 (optional) counts every console warning.** In dev mode, Vue, Vite or Quasar can log unrelated warnings, so "at most one `console` warning" can flake for reasons that have nothing to do with storage. | Count only messages of type `warning` whose text contains the plan's message ("can't be saved in this browser"), and expect exactly one. |
| 4 | Minor | **The `fakeStorage` stub has to type-check.** `.mockReturnValue(fakeStorage)` on the `sessionStorage` getter needs a full `Storage` (`length`, `key`, `clear`, `getItem`, `setItem`, `removeItem`). A partial object needs a cast, and the codebase otherwise avoids `as unknown as`. | Build `fakeStorage` as a complete `Storage` whose methods throw (a small object literal typed `Storage`, or a class implementing it). Don't cast. |

**Proportion and executability:** right-sized for a P1 story. That's 12 unit tests (mostly `it.each` tables)
and 8 required E2E specs, at about 55–70 minutes. The file list is complete (`src/composables/` is a new
folder). The step order keeps lint, types, unit and E2E green. The intermediate commit after step 2 has no
UI exit, but that stays on the branch and no step 2 spec needs it.

---

## 3. Implementation notes  *(pizza-developer)*

### Round 1
Implemented plan Round 2, steps 1-4, with review round 2's four Minors folded in.
- **Commits:**
  - `bc23063` Add draft-order store with sessionStorage persistence
  - `6ff4a2c` Add kiosk start screen, order page and draft route guard
  - `72e23f2` Add Cancel order with confirmation to the order builder
  - `447ec2b` Cover tablet touch targets in kiosk E2E
- **Deviations from the plan, and why:**
  - Optional E8 (throwing `window.sessionStorage` getter in the browser) was **dropped**: it timed out
    because the page never rendered Start order with the getter throwing, so it wasn't green first time. U4 and
    U10 cover the same behaviour at unit level. The step 4 commit subject is shortened accordingly ("Cover
    tablet touch targets in kiosk E2E"). I did not investigate which code probes `sessionStorage`.
  - The unit tests are 31 tests across 3 files (persistence, store, guard) (some `it.each` tables), and the E2E suite is 8 kiosk specs
    (E1-E7 and E9; E1's Cancel part and E2/E3 are separate `test`s) plus the 2 smoke specs. The wording and
    grouping of the E2E tests differs slightly from the table (e.g. E4 seeds a draft after Start order).
  - `parseStoredDraft` builds the result directly rather than via `createEmptyDraft()` (same shape).
  - `routes.ts` carries the `RouteMeta` augmentation, as planned. The 404 stays outside `MainLayout`.
- **Minors folded in:** M1 (methods called on the persistence object, parsed storage typed `unknown`, no
  destructuring), M2 (Start order asserted >= 96 px, Cancel >= 48 px), M3 (n/a, E8 dropped), M4 (the fake
  `Storage` is a complete object literal, no cast).
- **Checks run** (from `client/`, after each step; final state below):
  - `npx prettier --check --end-of-line auto <changed files>` -> clean
  - `npx eslint -c ./eslint.config.js "./src*/**/*.{ts,js,cjs,mjs,vue}" test/e2e test/vitest` -> exit 0
  - `npm run typecheck` -> clean
  - `npm test` -> 3 files, 31 tests passed
  - `npm run test:e2e` -> 10 passed (8 kiosk + 2 smoke)
  - `npx quasar build` -> succeeded
- **Follow-ups / proposed decisions:** none beyond D-044/D-045 as planned. `useDraftOrderStore(store)` is called
  inside `beforeEach` (the Quasar `store` argument was defined). The orchestrator still owns the README lines.

---

## 4. Code review  *(pizza-reviewer)*

### Round 1
**Verdict:** PASS

Reviewed `git diff origin/main...HEAD -- client/` (commits `bc23063`, `6ff4a2c`, `72e23f2`, `447ec2b`) against
plan Round 2, plan review Round 2's four Minors and issue #25. The code matches the plan closely. It's small and
readable, and the tests would fail if the behaviour broke. No Blockers or Majors.

**Traceability** (AC 4, the idle timeout, is out of scope by the user's choice and moved to #44)
| AC | Implemented in | Proven by | Status |
|---|---|---|---|
| 1. Welcome screen with a large **Start order** button | `src/pages/StartPage.vue` (`size="xl"`, `min-height: 96px`, max 480 px), route `start` in `src/router/routes.ts` | E1 `kiosk-start.spec.ts:34`, E9 `:149` (≥ 96 px tall, inside 768 px, tap works) | ✅ |
| 2. **Cancel order** always available while building; confirms, clears the draft, returns to start | `src/components/CancelOrderButton.vue` (Dialog plugin, `focus: 'cancel'`), `src/composables/use-end-order.ts` (clear, then push `start`), `src/layouts/MainLayout.vue` (`v-if="route.meta.building"`), `src/router/kiosk-guard.ts` + `src/router/index.ts`, `quasar.config.ts` (`Dialog`) | E1 `:34` (hidden on `/`, visible on `/order`), E2 `:47` (Keep and Escape keep the order), E3 `:68` (confirm → `/`, key removed, Back stays on `/`, start over is empty), E9 (≥ 48 px), U6, G1/G2 | ✅ |
| 3. Draft in a Pinia store; a refresh keeps it (sessionStorage) | `src/stores/draft-order-store.ts` (setup store, load in setup, sync deep watch → save), `src/stores/draft-order-persistence.ts` (versioned envelope, shape-validated, warn once), `src/models/draft-order.ts` (codes only, no money, D-039) | U1–U4 (persistence), U5, U7–U10 (store), E2's reload (the app's own save survives), E4 `:93` (seeded line restored → "2 pizzas"), E5 `:105`, E7 `:127` (corrupt value ignored and removed) | ✅ |

Count: **3 ✅, 0 ⚠️, 0 ❌**.

**Plan conformance:** all four steps are done, in order, one commit each. Each has an imperative subject,
`Refs #25`, and a `Co-Authored-By: Claude Sonnet 5.5` trailer. The file list matches 1.3 exactly: no server
change, no new dependency, no `README.md` edit, no unplanned file. Plan review Round 2's Minors: M1 (methods
called on the persistence object, parsed storage typed `unknown`), M2 (Start order ≥ 96 px, Cancel ≥ 48 px)
and M4 (complete `Storage` literal, no cast) are folded in. M3 lapses because E8 was dropped. The deviations in
section 3:
- **E8 dropped: acceptable, and it doesn't hide an app bug.** I traced the cause. In `quasar dev`, Vite serves
  Quasar's whole client bundle, and its `SessionStorage` plugin runs `getStorage("session")` at module scope
  (`node_modules/quasar/dist/quasar.client.js:30836`, which reads `window[type + "Storage"]` at `:30626`). The
  only guard is `client.has.webStorage`, and that checks `window.localStorage` alone (`:226`). With only the
  `sessionStorage` getter throwing, Quasar's module throws on evaluation and the app never mounts, which is the
  timeout the developer saw. In the production build that plugin is tree-shaken out: `dist/spa/assets/index-*.js`
  only touches `sessionStorage` in vue-router's scroll-position helpers (both wrapped in `try/catch`) and in our
  `draft-order-persistence.ts` (wrapped in `try/catch`). The app's own code never throws on a failing getter,
  and U4/U10 prove that. I reasoned this from the code and the built bundle; I didn't run the production build
  in a browser.
- 31 unit tests (16 persistence, 7 store, 8 guard) in place of "12" (it.each rows count separately): acceptable.
  Every U1–U10, G1 and G2 row of 1.5 is present, and U2's table matches the plan's list case for case.
- `parseStoredDraft` builds its result directly; E4 seeds after Start order; E1–E3 are separate `test`s:
  acceptable, same behaviour.

**Correctness notes (no finding):** `endOrder()` clears the draft before it pushes, so the guard lets `/`
through. E3 proves it, because otherwise the push would bounce back to `/order`. Each guard redirect target
passes the guard under the same `hasDraft` (G2), so there's no loop. The store lookup stays inside
`beforeEach`, as planned. `parseStoredDraft` copies only the known fields and copies `toppingCodes`, so no stray
keys or shared arrays reach the store. The draft holds no money (D-039), one promo code (D-043). No debug
leftovers. The one `console.warn` is intentional and fires once.

**Checks re-run** (from `client/`, on `447ec2b` + the docs commit):
- `npm run typecheck` → exit 0
- `npx eslint -c ./eslint.config.js "./src*/**/*.{ts,js,cjs,mjs,vue}" test/e2e test/vitest` → exit 0
- `npm test` → 3 files, **31 / 31 passed**
- `npm run test:e2e` → **10 / 10 passed** (8 kiosk + 2 smoke, Chromium, own dev server on :9100)
- `npx prettier --check --end-of-line auto` on the 16 changed client files → "All matched files use Prettier code style!"
- `npx quasar build`: not re-run (not in this round's brief). A `dist/spa` from 21:20 today is present, and I
  inspected it for the E8 analysis.

| # | Severity | File:line | Finding and failure scenario | Suggested fix |
|---|---|---|---|---|
| 1 | Minor | `src/components/CancelOrderButton.vue:26`; `test/e2e/kiosk-start.spec.ts:47` | `focus: 'cancel'` is a stated safety property in D-044 ("a stray Enter keeps the order"), but no test covers it. If someone drops the option or swaps the button order, Enter on the open dialog would cancel the customer's order, and every spec would still pass. | In E2, after the Escape case, add: Cancel order → `page.keyboard.press('Enter')` → dialog hidden, still `/order`. That's 3 lines. |
| 2 | Minor | `docs/plans/25-kiosk-start-screen.md` §3 (E8 bullet) | The note says the cause wasn't investigated, and a reader might assume memory-only mode is broken in the real app. The cause is Quasar's dev-mode bundle, not our code (see Plan conformance). | Orchestrator: when it writes the Outcome or WORKLOG, add one line: "E8 can't run under `quasar dev` (Quasar's SessionStorage plugin reads `window.sessionStorage` at load); prod build and app code are safe, and U4/U10 cover it." No code change. |

---

## 5. Outcome  *(orchestrator)*
- **Code review round 1: PASS** (3/3 ACs ✅). Both Minors handled by the orchestrator:
  - **Minor 1:** added a "stray Enter keeps the order" check to E2 (`toBeFocused` on Keep my order,
    then Enter). Mutation-checked: removing `focus: 'cancel'` makes it fail.
  - **Minor 2:** E8's timeout cause, per the reviewer. In `quasar dev`, Quasar's full client bundle
    reads `window.sessionStorage` at module load (its SessionStorage plugin), so a throwing getter
    stops the dev app mounting at all. The production bundle tree-shakes that plugin out. Our
    persistence is try/catch-safe and covered by U4/U10. Not an app bug.
- **Orchestrator re-ran:** typecheck ✅, ESLint ✅, Prettier (changed files) ✅, Vitest 31/31, Playwright
  10/10, `quasar build` ✅.
- **Decisions:** D-044, D-045. **Follow-up:** #44 (idle timeout), now on the cut list (D-046).
- PR: see the PR that closes #25.
