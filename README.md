# Pizza Order Designer

A pizza **order builder** (kiosk-style web UI) and a separate **order service** (HTTP API) for the
take-home in [docs/ASSIGNMENT.md](docs/ASSIGNMENT.md). The customer builds an order, watches a running
total computed by the server, submits it (safe to retry on a flaky connection), then tracks it and can
change or cancel it until the kitchen starts it.

| Part | Tech | Where |
|---|---|---|
| Order builder (UI) | Quasar 2 + Vue 3.5 + TypeScript, Pinia, Vue Router | [`client/`](client/) |
| Order service (API) | ASP.NET Core on .NET 10, controllers | [`server/src/PizzaOrderDesigner.Api`](server/src/PizzaOrderDesigner.Api) |
| Storage | EF Core 10 + Npgsql → PostgreSQL 17, code-first migrations | [`server/src/PizzaOrderDesigner.Data`](server/src/PizzaOrderDesigner.Data) |

The UI and the API are separate processes that talk over HTTP. In Docker, nginx serves the UI and proxies
`/api` to the API container.

## Run it

**Everything (Docker only):**

```bash
docker compose up --build
```

- Order builder: http://localhost:8080
- API: http://localhost:5080 (for example `GET /api/health`, `GET /api/menu`)
- The API creates and seeds the database on startup. In compose, the kitchen auto-advances each order
  every ~20 s. Use the endpoint below to advance one on demand.

**Local development** (the .NET 10 SDK, Node 24, and Docker for Postgres):

```bash
docker compose up -d db
```

```bash
dotnet run --project server/src/PizzaOrderDesigner.Api
```

```bash
npm --prefix client ci
```

```bash
npm --prefix client run dev
```

The UI runs on http://localhost:9000 and proxies `/api` to http://localhost:5158.

## Tests

```bash
dotnet test server/PizzaOrderDesigner.slnx
```
Backend unit and integration tests. The integration tests start a throwaway Postgres in Docker
(Testcontainers), so **Docker must be running**.

```bash
npm --prefix client test
```
UI unit tests (Vitest).

```bash
npm --prefix client run test:e2e
```
UI end-to-end tests (Playwright, with a mocked API). First time only:
`npx --prefix client playwright install chromium`.

## Order service API (short)

Full contract with request and response examples: **[docs/API.md](docs/API.md)**. Runnable requests are in
[`PizzaOrderDesigner.Api.http`](server/src/PizzaOrderDesigner.Api/PizzaOrderDesigner.Api.http).

| Endpoint | Purpose |
|---|---|
| `GET /api/menu` | Sizes, topping categories, per-size topping prices |
| `POST /api/orders/quote` | Price a draft without saving it (the running total) |
| `POST /api/orders` + `Idempotency-Key` | Submit, safe to retry. Body = draft + `expectedTotal` |
| `GET /api/orders/{id}` | Order, status and priced snapshot, with an `ETag` |
| `PUT /api/orders/{id}` + `If-Match` | Change the order while it's Not Started |
| `DELETE /api/orders/{id}` | Cancel (deletes) while Not Started |
| `POST /api/kitchen/orders/{id}/advance` | Restaurant side: move to the next status |

```bash
curl -X POST http://localhost:5080/api/orders -H "Content-Type: application/json" -H "Idempotency-Key: 7d3c…" \
  -d '{"lines":[{"sizeCode":"large","toppingCodes":["pepperoni","bacon"],"quantity":2}],"promoCode":"ILOVEPIZZA","tip":{"mode":"percent","value":"15"},"expectedTotal":31.23}'
```

```bash
curl -X POST http://localhost:5080/api/kitchen/orders/<id>/advance
```

Errors are RFC 7807 ProblemDetails, with a customer-friendly `title`/`detail`, a stable `code`, field
`errors`, and the `traceId` that appears in the server log.

## Assumptions

Each one is recorded with its reasoning in [docs/DECISIONS.md](docs/DECISIONS.md).

**Pricing (D-038):**
- Money is `decimal` everywhere and rounded **half up** (half a cent rounds away from zero), explicitly.
  .NET's default is banker's rounding.
- A topping costs *category rate × the size's base price*, rounded to cents **per topping**.
- A line costs *(base + toppings) × quantity*.
- **Promo discounts apply to the subtotal**, are rounded once, and are capped at the subtotal.
- **Tax is 6.5% of (subtotal − discount)**, rounded **once per order**, not per line. Store-funded
  discounts reduce the taxable amount.
- **The tip isn't taxed.**
- **A percentage tip is calculated on the pre-tax amount after the discount** (Toast's default and common
  etiquette). "Percentage of the total bill" could also be read as after tax. It's one setting:
  `Pricing:TipBase = PreTax | PostTax`.
- **SAVE5 needs a subtotal strictly over $20.00** (before discount, tax and tip). Exactly $20.00 doesn't
  qualify.

**Validation (D-043):**
- One promo code per order. Codes are trimmed and case-insensitive.
- An ineligible SAVE5 is **rejected** with a message, not silently ignored.
- Limits: 1–25 lines per order; quantity 1–20; no duplicate topping on one pizza; tip $0–$500 or 0–100%,
  at most 2 decimals.

**Lifecycle (D-040, D-047):**
- The statuses are exactly Not Started → Started → Ready for Pickup → Delivered: linear, no skipping, no
  going back.
- **Cancelling deletes the order** (`GET` then returns 404). Cancelling or changing an order is allowed
  only while it's Not Started.
- **The server is the single source of truth for money** (D-039). Submit and update re-price the order and
  reject a total the customer didn't see (409 `price.changed`).
- **One order per `Idempotency-Key`.** A retry returns the original order. The same key with a different
  body is rejected (422). A retry of an order that was since cancelled returns 409 and never re-creates
  it.

**Kiosk:**
- An order in progress lives in the browser tab (sessionStorage). Refreshing keeps it, closing the tab
  discards it, and **Cancel order** always asks for confirmation.
- If a submit's outcome is unknown (connection lost, timeout, 5xx), the order **locks** and only
  **Retry** is offered. Retry resends the same request with the same key, so it can't create a second
  order. This survives a refresh (D-048).
- Editing an order after it's been placed isn't built (cut). The customer can still cancel it while it's
  Not Started.

**No auth:** one restaurant and anonymous customers, as the brief says. The kitchen endpoint is
unprotected.

## Design write-up

**Money and the total.** Every amount is a C# `decimal` and a Postgres `numeric(12,2)`. Rates are
`numeric(7,4)`, and nothing is ever a float. One helper, `Money.RoundToCents`, does all rounding (half up,
always two decimal places), so JSON shows `5.00`, never `5`. All pricing lives in one pure class,
`OrderPricer` (no I/O, no clock), which runs **only in the order service**. The UI shows the result of
`POST /api/orders/quote`, debounced as the customer edits, and never does arithmetic. The same pricer
prices quote, submit and update. On submit the client sends the total it displayed, and the server
rejects a mismatch with `409 price.changed`. So "the total you see is the total you're charged" holds by
construction, not by keeping two implementations in sync. The trade-off is a round trip per change, which
is fine for a kiosk.

**Extensibility.** The menu is data: sizes, topping categories (with a rate), toppings and promo codes
are database rows seeded by a migration. Pricing never names a size or topping.
- **A new size, topping or category** is an insert.
- **A new promo of an existing kind** (another percent-off code, another "$X off over $Y") is a row
  naming a rule type and its parameters.
- **A new kind of promo** is one new `IPromotionRule` class (`Evaluate(promotion, context) → eligible +
  discount`) registered in DI. Its own conditions live in that class.
- **Conflicts** are data: promos that share an exclusion group can't be combined (SAVE5 and ILOVEPIZZA
  share one).

The pricer only asks the promotion engine for a discount, so none of these touch it. Tests add a size, a
category and two promos as data and price them with no code change.

**Order lifecycle and guarantees.**
- *A retry after a timeout:*
  1. The UI creates an `Idempotency-Key` per checkout attempt and reuses it for automatic retries.
  2. The server stores the key, as a primary key, with a hash of the request **in the same transaction**
     as the order.
  3. A retry finds the key and returns the original order (200). If two copies arrive at once, the
     database lets only one insert commit, and the loser reads back the winner's order.
  4. The record has no foreign key to the order, so it outlives a cancellation. A late retry after a
     cancel gets 409 instead of resurrecting the order.

  Guarantee: **at most one order per key, whatever the timing.**
- *A cancel at the moment the kitchen starts:* each is **one conditional SQL statement**,
  `DELETE … WHERE id = @id AND status = 'NotStarted'` versus `UPDATE … SET status = 'Started' WHERE … AND
  status = 'NotStarted'`. Postgres row-locks the order, the second statement waits, then re-checks its
  `WHERE` against the committed row. **Exactly one succeeds:** either the order is gone and the kitchen gets
  404, or it's Started and the customer gets 409 "already started". Tests hold one side's lock in an open
  transaction to force each ordering. Updates use the row version (`xmin`) as an ETag with `If-Match`, so
  an update loses cleanly if anything changed underneath it.

**What I cut or faked, and what's next.** See the cut list below. The kitchen is simulated (an endpoint,
plus an optional timer in compose). Payment and auth aren't built. Editing a placed order works in the API
(`PUT` + `If-Match`) but has no UI. Next I'd add:
- that edit UI,
- a kitchen screen,
- CI running all three test suites,
- a Playwright suite against the real API in compose,
- structured JSON logging,
- running migrations as a deploy step instead of at app startup.

## Tests: invariants chosen

| Invariant | Why | Where |
|---|---|---|
| Every amount is whole cents with exactly 2 decimals; total = subtotal − discount + tax + tip; line sums hold; discount in [0, subtotal]; tax = round((subtotal − discount) × rate); repricing is identical | The brief's "exact arithmetic, consistent rounding, one source of truth" | `PricingInvariantTests`: 500 generated orders × both tip bases |
| Half-up rounding at midpoints (0.005 → 0.01, 0.125 → 0.13) | The classic .NET banker's-rounding trap | `MoneyTests` |
| SAVE5 boundary ($20.00 no, $20.01 yes), conflicts, case-insensitive codes, data-only new promos | Promo rules are the most ambiguous part of the brief | `PromotionEngineTests` |
| One order per key: sequential retry, 10 concurrent duplicates, key reuse 422, no resurrection after cancel | "Customers… may see timeouts and retry" | `OrderLifecycleTests` (real Postgres) |
| Cancel vs kitchen start: exactly one winner, in **both** orderings | The race the brief asks about | `RaceGuaranteeTests` (lock-holding, deterministic) |
| Only Not Started orders change; ETag/If-Match; linear advance | Explicit state transitions | `OrderLifecycleTests` |
| Unexpected errors don't leak internals; validation errors are field-level ProblemDetails | Error contract | `ErrorContractTests`, `QuoteEndpointTests` |
| Kiosk: draft survives refresh, corrupt storage is ignored, Cancel confirms, guard redirects | UI integrity | Vitest + Playwright (`kiosk-start.spec.ts`) |
| UI: the running total comes only from the server (debounced, stale responses dropped); a dropped submit is retried with the **same** key; a lost response **locks** the order until Retry (one order, one key, survives reload); price.changed → confirm → a **new** key | The UI side of the guarantees | Playwright (`order-*.spec.ts`, against a mock aligned with `docs/API.md`) + Vitest (`quote-store`, `api-client`) |

**Totals:** backend **100** (xUnit, including Postgres integration tests), UI **42** Vitest + **29**
Playwright. The key guarantees were mutation-checked: breaking each one makes its tests fail.
- Tax before discount: 5 failures.
- No scale-2 money: 4.
- An unconditional DELETE: 2.
- No unique-violation replay: 1.
- No status check on advance: 1.
- An update missing the FK race handling: 1.
- The UI forgetting the key after an unclear failure: 2.

**Not covered (time):**
- *The auto-advance timer:* it uses the same tested advance path, and was checked by hand in compose.
- *UI against the real API in an automated test:* the Playwright specs use a mock. The real stack was
  verified by hand: the worked example comes out at $31.23 end to end.
- *A 404-on-cancel UI test, and a tablet-width test for the builder.*
- *Visual and accessibility testing, and load testing.*

## Cut list (D-046)

| Cut | Note |
|---|---|
| #31 (part) Editing a placed order in the UI | The API supports it (`PUT` + `If-Match`, 409/412 tested) |
| #44 Kiosk idle timeout | The design is ready: `endOrder()` is the shared exit path |
| #29 Simulated payment | Not required, and the brief forbids real payments |
| #33 Kitchen view | The advance endpoint plus curl, and the compose auto-advance timer, cover it |
| #23 Structured JSON logging | Built-in logging with traceIds is in place |
| #24 OpenAPI UI | `/openapi/v1.json` is served in Development, and `docs/API.md` + `.http` cover the rest |
| #36 Frontend component tests | Playwright covers the UI behaviour |
| #38 CI | Tests run locally with one command each |

## AI usage note

**Tools.** Claude Code (Claude Opus 5.5) was the main driver: scaffolding, the backend, the docs, and
web research into how POS systems handle tip bases and tax on discounts (that research shaped D-038).
Subagents with written contracts ([docs/SUBAGENTS.md](docs/SUBAGENTS.md)) did planning and independent
**code review** on opus, and UI implementation on sonnet. Every PR got one independent review pass before
I saw it. All decisions, mine and the AI's, are logged in [docs/DECISIONS.md](docs/DECISIONS.md), and the
process in [docs/WORKLOG.md](docs/WORKLOG.md).

**AI output I rejected or had to fix.** The first concurrency test for "cancel vs kitchen start" fired
both HTTP requests in parallel and passed. When I logged which side won, the cancel had won **25 of 25**
rounds. The test never exercised "kitchen wins", and a deliberately broken `DELETE` (no status check)
still passed it. I replaced it with deterministic tests that hold one side's row lock in an open
transaction and prove the other side waits, then loses cleanly, in both orderings. The reviews also
caught real bugs in AI-written code that the AI's own tests had missed:
- an amount tip of "5" serialized as `5`, not `5.00`,
- an update losing a race to a cancel returned 500 instead of 404.

Both were fixed with tests that fail without the fix.

**An invariant I verified myself.** "The total is what the server charges, with exact rounding." Three
checks:
- **Mutation testing:** I made the pricer tax the pre-discount subtotal and watched 5 tests fail
  (including the 500-order invariant run). I also removed the scale-2 fix and watched 4 fail.
- **A documented trap:** I confirmed that .NET's default `Math.Round(0.125m, 2)` gives `0.12`, banker's
  rounding. A test documents it, and money uses `AwayFromZero`.
- **By hand, end to end:** I built the contract's worked example in the real UI against the real API and
  Postgres: 2 × Large (pepperoni, bacon), ILOVEPIZZA, 15% tip → **$31.23**. That matches the hand
  calculation (2.856 → 2.86, 1.6705 → 1.67, 3.855 → 3.86).

## Time spent

An honest account, from the work log (one long evening, about 19:20–22:30):

| Phase | Time |
|---|---|
| Workspace, tooling and process setup: SDK and Docker issues, repo, board, backlog, decision log, subagent workflow, M0 questions with research | ~1 h 50 m |
| Build and verify: #25 kiosk start, pricing core (PR A), order service (PR B), UI (PR C), reviews and fixes, README | ~1 h 20 m |

That's over the 3-hour box. Most of the overrun went on process, which ended up heavier than the
problem needed: a four-role subagent flow and an 871-line plan for one screen. I reset it partway through
(D-046): batched PRs, one independent review per PR, and the cut list above. The build phase, where the
graded work lives, took about 1 h 20 m. AI subagents ran several streams in parallel during that time.

## Working on the repo

`main` only changes through pull requests. Enable the repo's hook once per clone, so an accidental push
to `main` is rejected:

```bash
git config core.hooksPath .githooks
```

To add an EF Core migration, from `server/`:

```bash
dotnet ef migrations add <Name> -p src/PizzaOrderDesigner.Data -s src/PizzaOrderDesigner.Api
```

The process docs are [docs/DECISIONS.md](docs/DECISIONS.md) (every decision, D-001 to D-048),
[docs/WORKLOG.md](docs/WORKLOG.md) (what happened, session by session), [docs/SUBAGENTS.md](docs/SUBAGENTS.md),
and per-PR plans and reviews in [docs/plans/](docs/plans/).