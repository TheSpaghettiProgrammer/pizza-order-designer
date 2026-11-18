# PR B — Order service: plan (short, per D-046)

**Stories:** #15 persistence · #16 idempotent submit · #17 get/status · #18 update · #19 cancel (delete) ·
#20 state machine and race · #21 kitchen · #22 error contract · #35 integration tests.
**Branch:** `story/15-22-order-service`, stacked on PR A (it needs `OrderPricer` and `IMenuProvider`).
**Contract:** `docs/API.md` (already published). **Decisions:** D-039, D-040, D-042, D-043.

## 1. Data model (#15)
- `Order`:
  - `Id` (Guid)
  - `Status` (string enum: NotStarted, Started, ReadyForPickup, Delivered)
  - `CreatedAt`, `UpdatedAt`
  - the draft inputs: `PromoCode`, `TipMode`, `TipValue` (string)
  - a priced snapshot: `Subtotal`, `Discount`, `Tax`, `Tip`, `Total` (numeric(12,2)), plus `Pricing` (jsonb: the
    full `PriceBreakdown` at submit/update, so a later menu change never alters a placed order)
  - `Version`: Postgres `xmin` mapped with `IsRowVersion()`, sent as the ETag.
- `OrderLine`: `Id`, `OrderId` (FK, **ON DELETE CASCADE**), `SizeCode`, `Quantity`, `ToppingCodes` (`text[]`).
- `IdempotencyRecord`: `Key` (PK, ≤ 100 chars), `RequestHash` (SHA-256 of the canonical request), `OrderId`,
  `CreatedAt`. **No FK to orders.** It deliberately outlives a deleted order (D-040).
- One migration: `AddOrders`.

## 2. Submit, `POST /api/orders` (#16): the retry guarantee
1. `Idempotency-Key` missing → 400 `idempotency.key_missing`.
2. Look up the key. If found:
   - hash differs → **422** `idempotency.key_reused`
   - order exists → **200**, the original order (a replay)
   - order deleted → **409** `order.cancelled` (never re-create it).
3. Price with the shared `OrderPricer`. Invalid → 400 (nothing stored, so a corrected retry with the same
   key still works). `Total ≠ expectedTotal` → **409** `price.changed`, with the fresh quote.
4. In **one transaction**, insert the order + lines + idempotency record → 201 with `Location`.
5. **Concurrent duplicates:** both requests can pass step 2, but the idempotency PK lets only one insert
   commit. The loser catches the unique violation (`23505`), rolls back, re-reads the record and returns
   the winner's order (200). **Guarantee: one order per key, whatever the timing.**

## 3. Read and update (#17, #18)
- `GET /api/orders/{id}` → 200 with `ETag: "<xmin>"`, or 404 `order.not_found`.
- `PUT /api/orders/{id}`, `If-Match` required (missing → 428):
  - load the order
  - `If-Match ≠ xmin` → **412** `order.version_mismatch`
  - status ≠ NotStarted → **409** `order.already_started`
  - re-price (400 / 409 `price.changed` as for submit)
  - replace the lines and the snapshot, then `SaveChanges`.

  The `xmin` concurrency token makes the write conditional. If the kitchen advanced the order (or anyone
  changed it) since we read it, EF throws `DbUpdateConcurrencyException`. We re-read and return 409 if it
  started, 404 if it's gone, otherwise 412.

## 4. Cancel = delete, and the race (#19, #20): the race guarantee
- Transitions live in one table: `OrderStatusFlow.Next(status)`: NotStarted→Started→ReadyForPickup→
  Delivered, and Delivered has no next status.
- **Cancel:** `ExecuteDeleteAsync(o => o.Id == id && o.Status == NotStarted)`, a single SQL `DELETE … WHERE`.
  1 row → 204. 0 rows → re-read: exists → 409 `order.already_started`, else 404.
- **Kitchen advance** (#21): read the status, compute next, then
  `ExecuteUpdateAsync(o => o.Id == id && o.Status == current, set Status = next, UpdatedAt = now)`.
  0 rows → re-read: gone → 404, already moved → retry once from the new status, Delivered → 409
  `order.already_delivered`.
- **Why exactly one wins.** Postgres takes a row lock for both the DELETE and the UPDATE. The second
  statement waits, then re-checks its `WHERE` against the committed row (READ COMMITTED). So if the
  kitchen's `UPDATE … WHERE status = 'NotStarted'` commits first, the `DELETE … WHERE status = 'NotStarted'`
  matches 0 rows and the customer gets 409. If the delete wins, the advance matches 0 rows and the kitchen
  gets 404. Neither can see a half state.
- **Optional auto-advance (D-042):** a `BackgroundService` behind `Kitchen:AutoAdvance:Enabled` (false by
  default, true in docker-compose) with `IntervalSeconds`. Each tick advances orders whose `UpdatedAt`
  is older than the interval, through the **same** conditional advance.

## 5. Error contract (#22)
- Every error is a ProblemDetails with a friendly `title`/`detail`, a stable `code`, and `traceId`.
  Endpoints map results to status codes in one place (`OrderProblems`), which also holds the customer
  wording.
- Global `UseExceptionHandler`: 500 "Something went wrong on our side. Please try again." plus `traceId`.
  The full exception is logged with the traceId. Stack traces never reach the client.
- Key events logged at Information: submitted, replayed, updated, deleted, advanced, price-changed.

## 6. Tests (#35): real Postgres via Testcontainers
`WebApplicationFactory` + `Testcontainers.PostgreSql` (one container per test class; migrations applied).
These need Docker, which is the documented requirement.

| Test | Proves |
|---|---|
| Submit, then the same key and body again → same id, 200, 1 row | Retry after a timeout is safe |
| 10 parallel submits with the same key → 1 order, all responses share its id | Concurrent duplicates |
| Same key, different body → 422 | Key reuse is caught |
| Submit, delete, then submit with the same key → 409 `order.cancelled`, 0 orders | Deleted orders never come back |
| `expectedTotal` off by 0.01 → 409 `price.changed`, nothing stored | What you see is what you're charged |
| GET → ETag; PUT with a stale ETag → 412; PUT after advance → 409 | Update rules |
| Delete after advance → 409; delete twice → 204, then 404 | Cancel rules |
| **Delete vs advance, raced 25×** on fresh orders → exactly one succeeds every time, and the final state matches the winner | The race guarantee |
| Advance ×3 → Delivered; advance again → 409 | Linear flow, no skipping |
| Unhandled exception → 500 ProblemDetails with traceId, no stack trace | Error contract |

## 7. Out of scope (cut list, D-046)
The kitchen view (#33), structured-logging extras (#23), and an OpenAPI UI (#24). The `.http` file and
`docs/API.md` cover the API.

**Estimate:** about 60–80 min. **Risks:** `xmin` mapping in Npgsql 10 (fallback: an explicit `uint Version`
row-version column), and the Testcontainers start-up time (about 5 s per class).
