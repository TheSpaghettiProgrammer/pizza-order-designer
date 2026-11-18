# PR A: pricing core — code review

**Stories:** #8, #9, #10, #11, #12, #13, #14, #34 · **Branch:** `story/8-14-pricing-core` · **Base:** `origin/main`
**Process:** D-046 (main session implemented directly; no plan file, so sections 1–3 don't exist).

## 4. Code review  *(pizza-reviewer)*

### Round 1
**Verdict:** CHANGES REQUESTED (1 Major, 4 Minor). The money math matches D-038/D-043 exactly and the
design is clean and data-driven. The one Major is a cheap contract fix: an amount tip is sent as `2.5`
or `5` instead of a two-decimal number.

**Traceability** (39 ACs: 33 ✅, 6 ⚠️, 0 ❌)

| AC | Implemented in | Proven by | Status |
|---|---|---|---|
| #8 Entities Size / ToppingCategory / Topping (+ Promotion) | `Data/Menu/MenuEntities.cs`, `Configurations/MenuConfigurations.cs` | build; migration `InitialMenu` | ✅ |
| #8 Seeded by EF migration: S $5 / M $7 / L $12, Basic 8% / Premium 11%, 12 toppings | `Data/Menu/MenuSeed.cs` via `HasData`; `20261118051827_InitialMenu.cs` | `MenuSnapshotTests.Seed_matches_the_brief`, `Topping_price_is_category_rate_times_size_base` | ✅ |
| #8 `GET /api/menu`: sizes, toppings by category, price per size | `Controllers/MenuController.cs` | `QuoteEndpointTests.Menu_endpoint_returns_sizes_and_per_size_topping_prices` (and the main session's Postgres run) | ✅ |
| #8 Stable string codes, not DB ids | `MenuSnapshot` (lookups by code), all DTOs | `OrderPricerTests.Codes_are_case_insensitive_and_the_response_uses_menu_codes` | ✅ |
| #8 New size/category row changes prices with no code change | `MenuSnapshot.ToppingPrice` (data only) | `MenuSnapshotTests.A_new_size_and_category_are_priced_from_data_alone` | ✅ |
| #9 `decimal` / `numeric(12,2)`, rates `numeric(7,4)`, no float | `MenuConfigurations.cs` (`HasPrecision`), migration columns | grep: no `double`/`float` in the diff | ✅ |
| #9 One rounding helper, half-up | `Pricing/Money.cs:12`; the only `Math.Round` call sites are in `Money` | `MoneyTests` | ✅ |
| #9 API sends money with exactly two decimals | `PricingContracts.cs` (decimals, scale-preserving) | `QuoteEndpointTests.*two_decimal*` (only zero and computed amounts) | ⚠️ finding 1 |
| #9 Midpoint rounding tests | `Money.RoundToCents` | `MoneyTests.RoundToCents_rounds_half_away_from_zero`, `Default_Math_Round_would_be_wrong_for_money` | ✅ |
| #10 Pure `OrderPricer` → `PriceBreakdown` (per-line detail, subtotal, discount, tax, tip, total) | `Pricing/OrderPricer.cs`, `PricingContracts.cs` | `OrderPricerTests.Worked_example_from_the_API_contract` | ✅ |
| #10 Topping price = round(base × rate) from data | `MenuSnapshot.cs:67` | `MenuSnapshotTests` (3 sizes × 2 categories) | ✅ |
| #10 Order of operations per D-038 | `OrderPricer.cs:65-85` | worked example; `Tax_is_charged_on_the_subtotal_after_the_discount`; invariant 4 | ✅ |
| #10 Unknown size/topping → validation errors, not exceptions | `OrderPricer.PriceLine` | `Unknown_size_and_topping_and_duplicate_topping_are_field_errors` | ✅ |
| #10 Tests per size, per category, multi-line multi-qty | — | `MenuSnapshotTests`, `Plain_order_without_promo_or_tip`, invariant run | ✅ |
| #11 ILOVEPIZZA 10% before tax | `PercentOffOrderRule` + seed row | `PromotionEngineTests.ILOVEPIZZA_takes_10_percent_rounded_half_up` | ✅ |
| #11 SAVE5 strictly > $20.00 | `AmountOffOverThresholdRule` (`MinSubtotalExclusive`) | `SAVE5_requires_a_subtotal_strictly_over_20`, `SAVE5_boundary_on_real_menu_items` | ✅ |
| #11 No combining via an explicit conflict rule, not an `if` in the pricer | `PromotionEngine.cs:50-58` (exclusion group from data) | `SAVE5_and_ILOVEPIZZA_cannot_be_combined`, `A_new_promo_of_an_existing_type_is_data_only…` | ✅ |
| #11 Clear messages for unknown / ineligible / conflicting | `PromotionEngine`, rules | engine tests; HTTP 400 test (ineligible SAVE5) | ✅ |
| #11 Case-insensitive and trimmed | `PromotionEngine.cs:28-32`, `MenuSnapshot` comparer | `Codes_are_trimmed_and_case_insensitive` | ✅ |
| #12 Tax rate from config, not a magic number in the pricer | `PricingOptions.TaxRate`, `appsettings.json` | used by all pricer tests | ✅ |
| #12 Tip `{ mode, value }` validated per D-043 | `OrderPricer.ParseTip` | `Invalid_tips_get_clear_messages`, `Boundary_tips_are_accepted` | ✅ |
| #12 Tax on discounted amount; tip untaxed; % base configurable | `OrderPricer.cs:77-85` | `Tip_as_a_dollar_amount_is_added_untaxed`, `Tip_percent_base_is_configurable` (both modes) | ✅ |
| #12 Tests: zero, amount, percent, **percent with each promo**, midpoint | — | zero/amount/percent ✅, midpoint 3.855→3.86 ✅, ILOVEPIZZA+% ✅; **SAVE5 + % tip not tested** | ⚠️ finding 3 |
| #13 Line format `2 × Large pizza — Pepperoni, Bacon` / `no toppings` | `Pricing/OrderDescription.cs` | `OrderDescriptionTests`, `Description_lists_…` | ✅ |
| #13 Server-side from menu display names | `OrderPricer.PriceLine` uses `size.Name`, topping `Name` | `Description_lists_…` | ✅ |
| #13 In quote and `GET /api/orders/{id}` | quote ✅; order endpoint is PR B | — | ⚠️ PR B |
| #13 Unit test: zero toppings, several, qty > 1 | — | `OrderDescriptionTests.Line_lists_quantity_size_and_toppings` | ✅ |
| #14 `POST /api/orders/quote`, full breakdown + description, nothing persisted | `Controllers/OrdersController.cs` | `QuoteEndpointTests.Quote_returns_the_breakdown…` | ✅ |
| #14 Same `OrderPricer` as submit/update | singleton `OrderPricer` in DI; submit/update are PR B | — | ⚠️ PR B |
| #14 400 ProblemDetails with field errors and friendly messages | `OrdersController.InvalidOrder` (`code`, `traceId`) | `Invalid_order_is_a_400_problem_with_field_errors_code_and_trace_id` | ✅ (see finding 2 for binder 400s) |
| #14 `pricingVersion` / menu hash in the response | not built; D-039 instead uses `expectedTotal` on submit (API.md) | — | ⚠️ superseded by D-039; say so in the PR body |
| #34 Total = subtotal − discount + tax + tip, exactly | — | `PricingInvariantTests` invariant 2 (500 orders × 2 tip bases) | ✅ |
| #34 At most 2 decimals; consistent rounding | — | invariant 1 (value check only, see finding 1), `MoneyTests` | ✅ |
| #34 Quote total == submit total | determinism proven (invariant 5); submit is PR B | — | ⚠️ PR B |
| #34 SAVE5 $20.00 no / $20.01 yes; discount ≤ subtotal; never negative | — | engine boundary theory; invariant 3; `Discount_never_exceeds_the_subtotal` | ✅ |
| #34 SAVE5 + ILOVEPIZZA rejected; case-insensitive | — | `SAVE5_and_ILOVEPIZZA_cannot_be_combined`, case tests | ✅ |
| #34 Tip dollars vs percent; base as agreed | — | invariant run over both bases; `Tip_percent_base_is_configurable` | ✅ |
| #34 Extensibility as data (size, topping, category, promo of existing type) | — | `A_new_size_and_category_are_priced_from_data_alone`, `A_new_promo_of_an_existing_type_is_data_only…` | ✅ |
| #34 Consider property-based tests | seeded random generator (fixed seed, no FsCheck, so no new dependency) | `Every_valid_order_satisfies_the_pricing_invariants` | ✅ |

**Plan conformance:** No plan by design (D-046). Scope matches PR A. Notes, no action needed:
- The only new package is `Microsoft.AspNetCore.Mvc.Testing` 10.0.15 (test-only, on 10.0.x). Reasonable; mention it in the PR body.
- `fa4e58c` covers #10 and #12 in one commit (`Refs #10, #12`). D-046 asks for one commit per story. It's a defensible pair (one pricer), so I'm noting it, not asking for a split.
- `9504ca8` (D-046 process docs) rides in this PR, as D-046 allows.
- Known/intentional items confirmed: order/kitchen endpoints are specified only; migrate-on-startup unchanged; menu read per request.
- Math spot-check: I recomputed the API.md worked example by hand (14.28 → 28.56 → −2.86 → tax 1.6705→1.67 → tip 3.855→3.86 → 31.23) and the menu `pricesBySize` sample. Both are correct.

**Checks re-run** (from the worktree's `server/`):
- `dotnet build PizzaOrderDesigner.slnx` → Build succeeded, 0 warnings, 0 errors.
- `dotnet test PizzaOrderDesigner.slnx` → Passed 70, Failed 0, Skipped 0.
- Serialization probe (a scratch console app, System.Text.Json web defaults): `decimal.TryParse("2.5")` → `{"tip":2.5}`, `"5"` → `{"tip":5}`, `0.00m` → `0.00`. This confirms finding 1.
- Not run (per brief): docker, the API, the Postgres smoke.

| # | Severity | File:line | Finding and failure scenario | Suggested fix |
|---|---|---|---|---|
| 1 | **Major** | `Pricing/OrderPricer.cs:82`; `Pricing/Money.cs:12-13`; `PricingInvariantTests.cs:43` | **An amount tip isn't sent with two decimals.** The tip is the parsed customer text, so its scale is kept: `"2.5"` → `"tip":2.5`, `"5"` or `"0"` → `"tip":5` / `"tip":0` (confirmed by the probe). This breaks #9 AC3 and API.md ("exactly two decimals"), on a P0 input path. A UI that renders the raw number shows "$2.5". The totals are still right, because `total` takes scale 2 from `taxable`. Neither test catches it: invariant 1 checks the value (`HasAtMostTwoDecimals`), not the scale, and the HTTP test covers only zero and computed amounts. `Math.Round(x, 2)` never *raises* scale, so `RoundToCents(5m)` is still `5`. The same thing would happen to any promo `Amount` or `BasePrice` that a test or new data row supplies without scale. | Make `Money.RoundToCents` always return scale 2: `Math.Round(amount, 2, MidpointRounding.AwayFromZero) + 0.00m` (decimal addition takes the larger scale). Pass the amount tip through it (`=> Money.RoundToCents(tip.Value)`, lossless after validation), and the SAVE5 amount too. Tighten invariant 1 to `Assert.Equal(2, amount.Scale)`, and add an HTTP case with `{ "mode": "amount", "value": "5" }` asserting `"tip":5.00`. |
| 2 | Minor | `OrdersController.cs:19`; `OrderPricer.cs:49,103` | **Binder errors skip the error contract.** Malformed JSON, `"quantity": "two"` or `1.5`, or an empty body get the framework's automatic 400. It has no `code`, and its messages are "The JSON value could not be converted to System.Int32…", while API.md promises `code` on every error. `"lines": [null]` reaches `PriceLine` and throws a NullReferenceException → 500 (the stack trace shows in Development). | Set `ApiBehaviorOptions.InvalidModelStateResponseFactory` to add `code: "request.invalid"` and a friendly title. Treat a null line as a field error (`lines[i]`: "This item is incomplete."). Can be folded into #22 in PR B if preferred. |
| 3 | Minor | `OrderPricerTests.cs` / `PricingInvariantTests.cs:59-60` | **The SAVE5 + percent tip combination is untested** (#12 AC asks for "percent tip with each promo"). The invariants check tax but never the tip value. A tip computed on the pre-discount subtotal would only be caught by the single ILOVEPIZZA worked example. | Add `[Line("large",4)], "SAVE5", Percent("20")` → tip 8.60 (20% of 43.00), plus a PostTax variant. Optionally add invariant 6: `tip == amount ‖ round(base × %)`. |
| 4 | Minor | `Promotions/PromotionRules.cs:51`; `OrderPricer.cs:189,195` | **Messages depend on the server culture.** `${threshold:0.00}` and `{MaxTipAmount:0.00}` use the current culture, so a de-DE host prints "$20,00", and the tests that pin "$20.00" would fail there. | Format with `CultureInfo.InvariantCulture` (e.g. `threshold.ToString("0.00", CultureInfo.InvariantCulture)`). |

**For the PR body (no code change):** list #13 (`GET /api/orders/{id}`), #14 (same pricer as submit/update)
and #34 (quote == submit) as finished in PR B. Say that #14's `pricingVersion` AC is met by D-039's
`expectedTotal` / 409 `price.changed`, which catches any price change, menu edits included.

---

## Response and outcome  *(orchestrator)*

All findings were addressed in commit "Address PR A review". Per D-046 (one reviewer pass per PR), the
orchestrator verified the fixes itself instead of running a second review round.

| # | Finding | Resolution |
|---|---|---|
| 1 (Major) | An amount tip kept the customer's scale (`5`, `2.5`) | `Money.RoundToCents` now always returns scale 2 (`+ 0.00m`). The amount tip and the SAVE5 amount go through it. The invariant now asserts `Scale == 2`, and the generator types tips like "5" and "2.5". New HTTP theory: "5" → `5.00`, "2.5" → `2.50`. **Mutation check:** removing `+ 0.00m` fails 4 tests. |
| 2 | Binder/JSON errors had no `code`; `"lines":[null]` caused a 500 | `InvalidModelStateResponseFactory` → 400 "We couldn't read that request.", `code: request.invalid`. A null line is a field error ("This item is empty."), and null topping codes are skipped. Both are tested. |
| 3 | No test of SAVE5 + a percent tip | 4 × Large + SAVE5 + 20% → tip 8.60, total 54.40. |
| 4 | Culture-dependent money in messages | `Money.Format` uses InvariantCulture, as do the tip limit messages. |
| Notes | `pricingVersion`; #13/#14/#34 parts in PR B; new test package | Stated in the PR body. |

**Re-run:** `dotnet build` with 0 warnings; `dotnet test` **75/75**.
