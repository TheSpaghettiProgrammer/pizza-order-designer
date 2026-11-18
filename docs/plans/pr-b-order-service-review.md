# PR B: order service — code review

**Stories:** #15, #16, #17, #18, #19, #20, #21, #22, #35 · **Branch:** `story/15-22-order-service` · **Base:** `origin/main`
**Plan:** [`pr-b-order-service.md`](pr-b-order-service.md) (short plan, D-046). **Decisions:** D-039, D-040, D-042, D-043, D-047.

## 4. Code review  *(pizza-reviewer)*

### Round 1
**Verdict:** CHANGES REQUESTED (1 Major, 7 Minor). The core guarantees hold, and I checked them myself:
one order per key, a cancelled order is never re-created, and cancel vs. start always has exactly one
winner. The one Major is a race between update and cancel. I reproduced it: an update that loses that race
to a cancel returns **500** instead of **404**. No data is corrupted (the transaction rolls back), but
it breaks the contract in exactly the area the brief grades. The fix is a few lines plus one test.

**Traceability** (43 ACs: 35 ✅, 7 ⚠️, 0 ❌, 1 n/a)

| AC | Implemented in | Proven by | Status |
|---|---|---|---|
| #15 Order / OrderLine / toppings, tip input, idempotency key, concurrency token | `Data/Orders/OrderEntities.cs`, `Configurations/OrderConfigurations.cs`. Toppings are a `text[]` on the line, and the key lives in its own table (plan §1, D-040) | build, migration `AddOrders` | ✅ |
| #15 Priced snapshot (totals + per-line prices) stored at submit/update | `OrderService.Apply` (`OrderService.cs:229`): numeric columns plus `PricingJson` (jsonb). GET returns it as-is and never re-prices | `Submit_creates_the_order_with_its_priced_snapshot`; follows from the design | ✅ |
| #15 Status as a string enum; `xmin` as the concurrency token | `OrderConfigurations.cs:165,175`; migration `xmin xid rowVersion` | `Update_needs_the_current_ETag…` (412 after a change) | ✅ |
| #15 Unique index on the idempotency key | the PK of `idempotency_records` | `Concurrent_duplicate_submits_produce_exactly_one_order` | ✅ |
| #15 `IEntityTypeConfiguration<T>` + migration | `OrderConfigurations.cs`, `20261118052931_AddOrders.cs` | build | ✅ |
| #16 `POST` + `Idempotency-Key` → 201 + `Location` + order | `OrdersController.Submit`, `OrderService.SubmitAsync`. The body shape follows `docs/API.md` (`id`/`status`/`pricing`), not the AC's `orderId`/`breakdown` | `Submit_creates_the_order_with_its_priced_snapshot` | ✅ |
| #16 Same key + same payload → 200, the original order | `SubmitAsync:41-45`, `ReplayAsync` | `Retrying_with_the_same_key_returns_the_same_order…` | ✅ |
| #16 Same key + different payload → 422 | `ReplayAsync:78` | `Reusing_a_key_for_a_different_order_is_rejected` | ✅ |
| #16 Concurrent duplicates → exactly one order; the loser returns the winner's | PK + `catch … IsUniqueViolation` (`SubmitAsync:63-70`) | `Concurrent_duplicate_submits…` (10 in parallel). The main session's mutation check confirms the replay path is exercised | ✅ |
| #16 Re-price; mismatch → 409 `price.changed` with a quote | `PriceAsync:220` | `Submit_rejects_a_total_the_customer_did_not_see` | ✅ |
| #16 Missing key → 400 | `SubmitAsync:35` | `Missing_key_and_invalid_orders_are_400s_with_codes` | ✅ |
| #17 GET: status, lines, description, breakdown, timestamps + ETag | `OrdersController.Get`, `OrderResponse.From` | used by the update tests (ETag), plus the submit test's body | ✅ |
| #17 Unknown id → friendly 404 | `OrderProblems.NotFound` | `A_late_retry_never_recreates…` (GET 404), `Kitchen_advances…` (advance 404) | ✅ |
| #17 Safe to poll | `LoadAsync` (`AsNoTracking`, no writes) | inspection | ✅ |
| #18 PUT + `If-Match`: re-price and replace the snapshot | `UpdateAsync:102-148` | `Update_needs_the_current_ETag…` (27.60 total) | ✅ |
| #18 Past NotStarted → 409 | `UpdateAsync:120`, `:143` | `Update_needs_the_current_ETag…` (with a *fresh* ETag); see finding 2 | ✅ |
| #18 Stale ETag → 412 | `UpdateAsync:115` | `Update_needs_the_current_ETag…`; 428 when missing: `Update_without_If_Match_is_428` | ✅ |
| #18 Safe to retry (same body + ETag → same result) | not implemented: the retry of a successful PUT gets 412 | my probe P3: first PUT 200, identical retry 412 | ⚠️ finding 8 |
| #18 Status check and write are atomic | xmin-conditional `UPDATE` (`UpdateAsync:136`) | not tested. Probe P2 (advance in between) → 409 ✔. Probe P1 (cancel in between) → **500** ✘ | ⚠️ finding 1 (Major) |
| #19 DELETE only while NotStarted → 204 | `CancelAsync:156-158` | `Cancel_deletes_only_while_not_started` | ✅ |
| #19 Started or later → 409, friendly | `CancelAsync:167` | `Cancel_deletes_only_while_not_started` | ✅ |
| #19 Unknown / already deleted → 404 | `CancelAsync:167` | `Cancel_deletes_only_while_not_started` (delete twice) | ✅ |
| #19 One conditional `DELETE … WHERE status='NotStarted'` | `ExecuteDeleteAsync` (`CancelAsync:156`) | `RaceGuaranteeTests.Kitchen_start_holding_the_lock…`; the main session's mutation check | ✅ |
| #19 The idempotency record survives; a late retry → 409 `order.cancelled` | no FK on `idempotency_records`; `ReplayAsync:85` | `A_late_retry_never_recreates_a_cancelled_order` | ✅ |
| #19 Deletion logged with id, prior status, traceId | `CancelAsync:161` logs the id (the prior status is NotStarted by construction). The traceId only appears via log scopes; #23 is cut (D-046) | inspection | ⚠️ deferred |
| #19 Simulated payment (#29) | — | — | n/a |
| #20 One transition table, linear, others rejected (409) | `OrderStatusFlow.cs` | `OrderStatusFlowTests`, `Kitchen_advances_linearly_and_stops_at_Delivered` | ✅ |
| #20 Every status change is a conditional UPDATE; cancel a conditional DELETE; update uses NotStarted + token | `AdvanceAsync:187-189`, `CancelAsync:156`, xmin on update | the delete predicate and the update predicate are proven. The advance's own `status == current` predicate isn't guarded by any test (a known gap) | ⚠️ finding 3 |
| #20 Race: exactly one winner; start wins → cancel 409, delete wins → start 404 | the two conditional statements plus Postgres row locks (READ COMMITTED re-check) | `RaceGuaranteeTests` (both orderings, deterministic) | ✅ |
| #20 Concurrent cancel + start, many times, exactly one succeeds | — | `Cancel_racing_the_kitchen_start_has_exactly_one_winner` (25 rounds) | ✅ |
| #20 Transitions logged: id, from, to, actor, traceId | `AdvanceAsync:197` (no traceId field; #23 cut) | inspection | ⚠️ deferred |
| #21 `POST /api/kitchen/orders/{id}/advance` + optional timer behind a flag | `KitchenController`, `KitchenAutoAdvanceService`, `docker-compose.yml` | endpoint: `Kitchen_advances…`. The timer isn't tested (it's optional) | ✅ |
| #21 Same transition rules as #20 | the timer calls `OrderService.AdvanceAsync` | inspection | ✅ |
| #21 Documented in the README with example requests | `docs/API.md` has the curl example. The README is PR D (#37) | — | ⚠️ deferred |
| #22 Every error is ProblemDetails with title/detail/errors/code/traceId | `OrderApiControllerBase.Problem`, `InvalidModelStateResponseFactory` | `Missing_key…`, `ErrorContractTests`. Probe P5: a non-guid route gives an empty 404, and malformed JSON leaks .NET type names | ⚠️ finding 4 |
| #22 Global handler: friendly 500 + traceId, no stack trace | `Program.cs:26-36,55` | `ErrorContractTests.Unhandled_exception_is_a_friendly_500…` | ✅ |
| #22 Consistent 400/404/409/412/422 | `OrderProblems` | lifecycle tests | ✅ |
| #22 Wording in one catalog | `OrderProblems.cs` | inspection | ✅ |
| #35 WebApplicationFactory + real Postgres (Testcontainers) | `PostgresApiFactory.cs` | 96/96 passing | ✅ |
| #35 Sequential retry, N concurrent, 422 | — | the three #16 tests above | ✅ |
| #35 Update/cancel only while NotStarted (409 after); cancel idempotent | — | `Update_needs…`, `Cancel_deletes_only_while_not_started` | ✅ |
| #35 Race: concurrent cancel + start, exactly one winner | — | `Cancel_racing…`, `RaceGuaranteeTests` | ✅ |
| #35 Invalid transitions rejected; stale ETag 412 | — | `Kitchen_advances…` (409 after Delivered), `Update_needs…` | ✅ |

**Plan conformance**
- §1 data model, §2 submit, §3 read/update, §4 cancel, §5 error contract and §6 tests are all done as
  planned. The extra `OrderLine.Position` column is fine: it keeps line order stable.
- **Deviation:** advance doesn't retry after losing a race; it returns 409 `order.status_changed`
  (D-047, `docs/API.md`). **Acceptable, and better than the plan:** a retry would let one stale kitchen
  click move the order two steps.
- **Additions:** 428 `order.precondition_required`, 400 `request.invalid` and 500 `server.error`. All
  three are documented in `docs/API.md` under "Differences from the first draft". Acceptable.
- The `Testcontainers.PostgreSql` dependency is named in plan §6. Acceptable.
- §6 says "nothing stored" for the price-changed test, but the test doesn't assert it (finding 6).
- Commits: one per story area, each with `Refs #N` and a model trailer, plus the PR A merge commit.
  Clean.

**Idempotency, checked in detail (no findings)**
- **Hash stability:** the hash is SHA-256 of the `System.Text.Json` serialization of the bound
  `OrderRequest`. That fixes the property order and drops whitespace and unknown fields. Decimal scale
  is preserved, so `13.8` and `13.80` hash differently. That's fine: a retry resends the same bytes.
- **Unique-violation replay:** EF orders inserts by table name, so `idempotency_records` goes in first.
  The loser blocks on the winner's uncommitted primary key, gets `23505`, its transaction rolls back,
  and a fresh READ COMMITTED read returns the winner. A different body that loses the race still gets
  422. A deleted winner still gets 409.
- **Transaction boundary:** one `SaveChanges` covers the order, its lines and the record. A failed
  price check or validation stores nothing, so a corrected retry with the same key works.

**Kitchen timer, checked (no findings):** it's off by default. It uses one scope per tick and catches
per-tick exceptions (but not cancellation, so shutdown is clean). The interval is clamped to at least
1 s. Every step goes through the same conditional `AdvanceAsync`, so a duplicate instance or a parallel
endpoint call can't double-step or resurrect an order. `UpdatedAt` and the cutoff both come from the
same `TimeProvider`.

**Checks re-run** (from the worktree's `server/`, Docker running)
- `dotnet build PizzaOrderDesigner.slnx` → 0 errors, 7 warnings (finding 7).
- `dotnet test PizzaOrderDesigner.slnx` → **96/96 passed**, 0 failed, 0 skipped (8 s).
- Probe tests, run in a **throwaway copy** of `server/` in my scratchpad. Nothing was added to the
  worktree. A hooked `IMenuProvider` runs code between `UpdateAsync`'s read and its `SaveChanges`:
  - **P1**, cancel in between → **500** `server.error`. The log shows `23503 FK_order_lines_orders_OrderId`, a `DbUpdateException`.
  - **P2**, advance in between → 409 `order.already_started` (correct).
  - **P3**, PUT, then the identical PUT with the same ETag → 200, then 412.
  - **P4**, a stale advance against a committed NotStarted→Started → 409 `order.status_changed` (correct).
  - **P5**, `GET /api/orders/not-a-guid` → 404 with an empty body. Malformed JSON → 400
    `request.invalid` with `"The JSON value could not be converted to System.Int32…"` and
    `"request": ["The request field is required."]`. A 101-character key → 400 `idempotency.key_missing`
    ("missing its Idempotency-Key header"). `If-Match: *` → 412.

| # | Severity | File:line | Finding and failure scenario | Suggested fix |
|---|---|---|---|---|
| 1 | **Major** | `server/src/PizzaOrderDesigner.Api/Orders/OrderService.cs:133-145` | **An update that loses a race to a cancel returns 500, not 404.** `SaveChanges` runs `DELETE order_lines` (0 rows), then `INSERT order_lines`, then the xmin-checked `UPDATE orders`. Once the cancel has committed, the line insert fails first with FK violation `23503`. That surfaces as a plain `DbUpdateException`, which the `catch (DbUpdateConcurrencyException)` doesn't catch. Scenario: the customer taps "Save changes" and "Cancel" (or uses two tabs). The cancel commits between the update's read and its write, and the update gets "Something went wrong on our side" plus an error log, instead of 404 per `docs/API.md`. Data stays consistent (it rolls back). Reproduced (probe P1). Neither this path nor the advance-in-between path (P2) is covered by a test. | Catch `DbUpdateException` when it's a concurrency exception **or** `InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation }`. Keep the existing re-read mapping (404 / 409 / 412). Add two integration tests that hook `IMenuProvider` (or a service seam) to cancel or advance between the read and the write. Expect 404 and 409 `order.already_started`. |
| 2 | Minor | `OrderService.cs:115-123` | The version is checked before the status. A customer who loaded the order before the kitchen started always has a stale ETag, because the advance bumps `xmin`. So the real "edit after the kitchen started" case gets 412 "changed somewhere else", not the AC's 409 "kitchen already started". The `catch` at `:143` uses the opposite order. RFC 9110 allows 412 first, and the UI re-fetches anyway. | Check `CustomerCanModify` before the ETag, which makes both paths agree. Or keep the order and have the UI's 412 handler re-fetch and show the status. |
| 3 | Minor | `OrderService.cs:188`; `RaceGuaranteeTests.cs` | Known gap: no test fails if `&& o.Status == current.Value` is dropped from the advance. Without it, a stale advance returns 200 and logs a duplicate transition. In a longer interleaving (A reads NotStarted, then B and C advance to ReadyForPickup), A would move the status **back** to Started. | Add the deterministic test from probe P4. Hold `NotStarted→Started` in a transaction, run `AdvanceAsync` concurrently, commit, then assert 409 `order.status_changed` and that the status is still Started. |
| 4 | Minor | `server/src/PizzaOrderDesigner.Api/Program.cs:39-55` | Error-contract edges: a non-guid route (`/api/orders/abc`) returns an empty 404, not ProblemDetails. The malformed-JSON 400 includes `System.Int32` and the parameter name `request`, which are .NET internals in the `errors` map. | Add `app.UseStatusCodePages()` (it writes ProblemDetails via `AddProblemDetails`). Set `JsonOptions.AllowInputFormatterExceptionMessages = false` and/or drop the `request` key in the factory. |
| 5 | Minor | `OrderService.cs:35` | A key longer than 100 characters is reported as "missing its Idempotency-Key header". That misleads anyone debugging a client. | Use a separate detail, e.g. "The Idempotency-Key must be 1–100 characters", and add the limit to `docs/API.md`. |
| 6 | Minor | `server/tests/PizzaOrderDesigner.Api.Tests/Orders/OrderLifecycleTests.cs:104-112` | Plan §6 says "nothing stored" for a price mismatch, but the test asserts only the 409 and the quote. Similarly, nothing proves that a corrected retry with the same key then succeeds. | Retry the same key with the right `expectedTotal`, and assert 201 (proves that nothing, including the key, was stored). |
| 7 | Minor | `PostgresApiFactory.cs:13`; `OrderLifecycleTests.cs:211-225` | 7 build warnings: CS0618 (the parameterless `PostgreSqlBuilder()` is obsolete) and xUnit1031 (`.Result` after `WhenAll`). | `new PostgreSqlBuilder("postgres:17-alpine")`, and `var c = await cancel; var s = await start;`. |
| 8 | Minor | #18 AC "safe to retry"; `docs/API.md` PUT table | A PUT retried after a timeout, when the first attempt succeeded, gets 412, not "the same result" (probe P3). It *is* safe (no double effect), but the AC as written isn't met, and that isn't recorded anywhere. | Simplest: say so in `docs/API.md` and D-047 ("a PUT retry after success gets 412; re-fetch shows the change applied"). Optional: on a mismatch, return 200 if the current order is NotStarted and already matches the request. |

Deferred, not counted as findings: the traceId in transition and deletion logs (#19, #20) depends on #23,
and the README examples (#21) depend on #37. Both were cut or scheduled in D-046. Say so in the PR body
so the `Closes #19/#20/#21` lines don't over-claim.

---

---

## Response and outcome  *(orchestrator)*

Addressed in commit "Address PR B review". The fixes were verified by the orchestrator (D-046: one
reviewer pass per PR).

| # | Finding | Resolution |
|---|---|---|
| 1 (Major) | An update losing to a cancel → 500 (FK 23503) | `UpdateAsync` also catches a foreign-key violation and re-reads → 404, or 409 if started. New `InterleavingTests` use a hooked menu provider (called between the read and the save) to force **cancel-in-between → 404** and **advance-in-between → 409**, and check the losing update changed nothing. **Mutation:** removing the FK catch fails the test. |
| 2 | Version checked before status | Status is checked first, so an edit after a start → 409 `already_started` (matches the conflict handler). |
| 3 | No test for the advance's status predicate | `A_stale_kitchen_advance_does_not_double_step` holds kitchen A's lock and shows kitchen B waits, then gets 409 `status_changed`. **Mutation:** dropping the predicate fails it. This closes the known gap. |
| 4 | Empty 404 for a non-GUID id; type names in JSON errors | `UseStatusCodePages()` gives a ProblemDetails body. `AllowInputFormatterExceptionMessages = false`. Tested. |
| 5 | An over-long key was reported as "missing" | New code `idempotency.key_invalid`. Tested. |
| 6 | "Nothing stored on price.changed" not asserted | The same key is reused with the correct total → 201. |
| 7 | 7 build warnings | `PostgreSqlBuilder(image)`, and the race test awaits its tasks. **0 warnings.** |
| 8 | A PUT retried after success → 412 | Documented in `docs/API.md` and D-047 as safe behaviour. |
| Notes | traceId in logs (#23, cut); README examples (#21 → PR D) | Stated in the PR body. |

**Re-run:** `dotnet build` with 0 warnings and 0 errors; `dotnet test` **100/100**.
