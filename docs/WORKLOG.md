# Work Log

A chronological record of what was done in each working session, including problems hit and how they
were resolved. Decisions are summarized here and explained in [DECISIONS.md](DECISIONS.md).

---

## 2026-11-17 — Session 1: Workspace setup

**Goal:** Blank, runnable skeleton for the stack (D-001), before the assignment details are given.
Not a git repo yet; that comes later.

### Environment findings and fixes
- **.NET SDK:** only the .NET **8** SDK was installed → installed the .NET **10** SDK (10.0.404) with
  `winget install Microsoft.DotNet.SDK.10`. The first attempt failed with MSI error 1618 (another
  installer was running); the retry succeeded.
- **Node:** v22.16.0 / npm 11.4.1. `create-vue` declares `node ^22.18.0 || >=24.12.0`, which only
  produced a warning.
- **npm bug:** `npm install` crashed with `Cannot read properties of null (reading 'edgesOut')`, a known
  npm 11.4 peer-dependency bug. **Workaround:** `npx npm@latest install` (npm 12.2.3) succeeded. See
  open items.
- **Docker Desktop failed to start:** `initializing Inference manager ... remove ...\Docker\run\dockerInference:
  The file cannot be accessed by the system`. Cause: stale AF_UNIX socket reparse points from
  2026-01-31 in `%LOCALAPPDATA%\Docker\run` (`dockerInference`, `userAnalyticsOtlpHttp.sock`). Windows
  can't open them (error 1920), so `del` and `File.Delete` both fail. **Fix:** with Docker Desktop
  stopped, rename the folder (`run` → `run.stale-20261117`) so Docker recreates a clean `run`.
  The next start then failed the same way on `%LOCALAPPDATA%\docker-secrets-engine\engine.sock` →
  renamed that folder aside too (and `run` again, as `run.stale-20261117b`). Docker then started.
  **Root cause not fixed:** a socket Docker created minutes earlier was also unreadable after Docker
  exited, so this machine can't clean up AF_UNIX sockets and future restarts may fail again.
  Durable fix: reboot and update Docker Desktop (4.58.0 is ~10 months old). Delete the `*.stale-*`
  folders after a reboot.
- Docker Desktop restarted once more (7:38 PM) just after the first `compose up`. This was the user
  installing a Docker Desktop update (4.58.0 → **4.99.0**, engine 29.9.3).
- **After the update:** `docker desktop restart` came back cleanly with no stale-socket crash, so the
  update appears to have fixed it. The `*.stale*` folders under `%LOCALAPPDATA%` (`Docker\run.stale*`,
  `docker-secrets-engine.stale*`) are no longer needed and can be deleted (after a reboot, if Windows
  still refuses).
- A global `dotnet-ef` 10.0.5 exists. The repo uses a local tool manifest instead (D-008).

### Done
1. **Layout** (D-002): created `PizzaOrderDesigner/` with `client/`, `server/`, `docs/`.
2. **Frontend** (D-013): scaffolded `client/` with create-vue, installed packages, added a Vite `/api`
   proxy (D-011). `npm run build` and `vitest run` pass.
3. **Backend** (D-005 to D-009, D-014, D-015):
   - `global.json` (SDK 10.0.404), `PizzaOrderDesigner.slnx`, projects Api / Data / Api.Tests.
   - The first `dotnet add package` pulled EF Core 11 (net11-only) → pinned all EF packages to 10.0.15.
   - Added `PizzaDbContext` (empty model, applies `IEntityTypeConfiguration`s from the Data assembly),
     `AddPizzaData()` DI extension, health checks at `/api/health`, and config-gated migrate-on-startup
     (D-010).
   - Removed the WeatherForecast sample and HTTPS redirection.
   - `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 1/1 passing (placeholder).
4. **Containers** (D-003, D-012, D-016): `server/Dockerfile` (sdk → aspnet, non-root),
   `client/Dockerfile` (node 24 → nginx) + `nginx.conf` (SPA fallback, `/api` proxy),
   `.dockerignore` files, and root `docker-compose.yml` (db healthcheck gates api).
5. **Tracking**: this log, `DECISIONS.md`, root `README.md`, `CLAUDE.md` (working agreements for
   AI-assisted sessions), `.env.example`, root `.gitignore`.
6. **Container smoke test** (`docker compose up --build -d`):
   - db healthy → api → web started in order.
   - `GET :5080/api/health` → `200 Healthy` (API → Postgres check passes).
   - `GET :8080/api/health` → `200 Healthy` (nginx `/api` proxy works).
   - `GET :8080/` and `GET :8080/some/route` → 200 with the SPA shell (history-mode fallback works).
   - API logs showed `libgssapi_krb5.so.2` load errors → fixed with D-018, confirmed gone.
   - Expected log noise until the first entity/migration exists: "No instantiatable types implementing
     IEntityTypeConfiguration", and a failed `SELECT ... FROM "__EFMigrationsHistory"` (EF found no
     history table, then "No migrations were found").

7. **Re-verified on Docker Desktop 4.99.0:** `docker compose build --no-cache` + `up -d` → all three
   services up, both health URLs `200 Healthy`, SPA and deep-link fallback 200, no errors in the API
   log. After a Docker Desktop restart the stack was started again and was healthy. The containers
   don't auto-restart because no `restart:` policy is set (deliberate for local use).

8. **Session directory** (D-019): moved the session's working directory to `PizzaOrderDesigner/`.
9. **Frontend library request:** the user asked for the latest Quasar + Vue. Version research so far:
   Vue 3.5.45 is latest stable (3.6 is RC only). Quasar 2.35.5, `@quasar/app-vite` 3.10.4 (Vite ~8.3,
   requires Node `^22.22 || >=24`), `@quasar/vite-plugin` 2.0.2 (peer Vite ^8). Integration approach
   not decided yet.

10. **Quasar frontend** (D-020 to D-025). The user chose the Quasar CLI and Node 24.
    - **Node:** winget has no `OpenJS.NodeJS.24` package. Installed `OpenJS.NodeJS.LTS --version
      24.22.1`, since unpinned it would have installed 26. `npm --version` still showed 11.4.1 because
      of a global npm in `%APPDATA%\npm` → upgraded it to 12.2.3.
    - **Scaffold:** moved the old client to `client.old`, then ran `create-quasar`. The first two
      attempts failed: `--install npm` must be a bare `--install` under npx, and the `eslint` +
      `oxlint` presets are mutually exclusive.
    - Configured history routing and the dev proxy (port 9000), stripped the demo content, and added
      Vitest through the Quasar extension (applied by hand, see D-023). Carried over the Dockerfile
      (updated for `quasar build`), `nginx.conf` and `.dockerignore`. Deleted `client.old`.
    - create-quasar filled `package.json` → `author` from the global git config (name and email).
      Left for the user to decide whether to keep it.
    - **Verified:** `vitest run` 1/1, `vue-tsc --noEmit` clean, ESLint clean, `quasar build` OK
      (built `index.html` uses absolute `/icons/...` and `/assets/...` URLs).
    - **Container:** `docker compose up --build -d web` → `/` and `/some/route` 200 with the app
      shell, `/api/health` 200 Healthy through nginx, icons served.
    - **Host dev mode, now verified:** `dotnet run` (http profile, :5158) against the db container +
      `quasar dev` (:9000). `:9000/api/health` → 200 Healthy through the dev proxy. The page rendered
      in a browser with the Quasar header. Both dev servers were stopped afterwards.

### Not verified
- Nothing outstanding from this session.

### Open items (session 1)
- ~~Upgrade host Node~~ Done: Node 24.22.1 / npm 12.2.3 (D-021).
- ~~Decide on the auto-filled `author`~~ Done: the user chose to remove the email; `author` is now the
  name only.
- No EF migration exists yet. The first one is created when the first entities are modeled.
- Replace the placeholder test once real endpoints exist (D-017).
- ~~`git init` + first commit when the user is ready~~ Done in session 2.

---

## 2026-11-17 — Session 2: Repo, project board, backlog

**Goal:** Put the project in a private GitHub repo, set up a Project board, and turn the take-home brief
into reviewable stories and open questions. **No feature code yet.** The user reviews the backlog first.

### Input
- The take-home brief (`.docx`) → `docs/ASSIGNMENT.md` (D-026).
- The user's product vision: a modern self-order kiosk (begin order → browse/scroll options → complete
  or cancel → pay → send to kitchen), with errors shown to the customer as friendly text and logged in
  detail for developers.

### Key findings from the brief (they shape the backlog)
- **3-hour timebox.** *Judgment*, *proportionate design* and an honest cut list are graded, which
  conflicts with the full kiosk vision → question #1, plus the P0/P1/P2 labels.
- **Don't build:** real payments, a real kitchen, auth, menu admin, multiple locations. So "pay" can only
  be simulated (#5).
- **The graders focus on:** exact money and rounding with one source of truth (#2, #3); data-driven,
  extensible pricing and promos (#8, #11); safe retries (#16); explicit state transitions and the
  cancel-versus-kitchen-start race (#20); invariant-focused tests (#34, #35); README Assumptions and
  design write-up (#37).
- **Storage:** "any storage, no hosted DB required". Local Postgres in Docker fits, so no change.
- **Separate process requirement:** the "order builder" (Quasar SPA) and the "order service" (.NET API)
  already run as separate processes that talk over HTTP.

### Done
1. **git:** `git init -b main`. Checked the add list first (66 files, nothing from `node_modules`,
   `bin`/`obj`, `.quasar`, `dist` or `.env`). Initial commit "Initial project skeleton".
   - The first commit attempt failed because PowerShell passed the here-string as an argument
     instead of piping it to stdin → used `git commit -F <file>`.
2. **GitHub repo (D-027):** https://github.com/TheSpaghettiProgrammer/pizza-order-designer, **private**,
   `main` pushed.
3. **Labels and milestones (D-028):** 13 labels, 5 milestones (M0–M4).
4. **Backlog (D-028, D-029):** **38 issues**, created by a script so cross-references resolve to issue
   numbers.
   - **M0 Decisions (7 questions):** #1 scope vs timebox and kiosk vision · #2 order of operations and
     rounding · #3 where pricing runs · #4 cancelled status and linear flow · #5 simulated payment ·
     #6 kitchen simulation · #7 validation limits.
   - **M1 Menu & pricing (7):** #8 data-driven menu · #9 money and rounding · #10 pricing engine ·
     #11 promo rules engine · #12 tax and tip · #13 order description · #14 quote API.
   - **M2 Order service (10):** #15 persistence · #16 idempotent submit · #17 get/status · #18 update ·
     #19 cancel · #20 state machine and race guarantees · #21 kitchen simulation · #22 error contract
     (friendly vs developer) · #23 structured logging · #24 OpenAPI and examples.
   - **M3 Order builder UI (9):** #25 kiosk start screen · #26 menu and customizer · #27 cart and running
     total · #28 tip and promo inputs · #29 simulated payment · #30 retry-safe submit · #31 status
     tracking and update/cancel · #32 friendly UI errors · #33 kitchen view.
   - **M4 Quality & deliverables (5):** #34 pricing invariant tests · #35 lifecycle, retry and
     concurrency integration tests · #36 frontend tests · #37 README deliverables · #38 CI.
   - By priority (checked against GitHub): P0 = 30, P1 = 4 (#23, #24, #25, #38),
     P2 = 4 (#5, #29, #33, #36).
5. **Project board:** needs the `project` OAuth scope, which the `gh` token lacked
   (`gist, read:org, repo, workflow`). Started `gh auth refresh -s project` in a terminal tab for the
   user to finish the browser sign-in. Once the scope was granted, a script:
   - created the project **"Pizza Order Designer"**: https://github.com/users/TheSpaghettiProgrammer/projects/6
     (private),
   - linked it to the repo,
   - added a **Priority** single-select field (P0/P1/P2, mirroring the labels),
   - added all 38 issues, with Priority set from the labels.

   **Verified:** 38 items, all Status = Todo.
   The layout is the default table. Switching it to Board or grouping by Milestone has to be done in
   the web UI, because the API can't create views.

6. **Subagent guidelines and workflow** (D-030 to D-032), at the user's request:
   - `docs/SUBAGENTS.md`: principles, the roles table (model, tools, inputs, outputs, permissions), a
     workflow diagram (Mermaid) with the two user gates and loop limits, the 6-part briefing template,
     shared rules (context files, Windows environment, the check commands, conventions, the Never
     list), verdict and report formats, and when to escalate.
   - `docs/plans/_TEMPLATE.md`: the per-story plan file. Each role has its own section, and rounds are
     appended.
   - `.claude/agents/`: `pizza-planner`, `pizza-plan-reviewer`, `pizza-developer`, `pizza-reviewer`
     definitions, each pointing back to SUBAGENTS.md as its contract.
   - `CLAUDE.md`: a Subagents section. Root `.gitignore` now excludes `.claude/settings.local.json`.
   - Not yet exercised on a real story. The first story run will show whether the briefs and templates
     need adjusting. The agent definitions load at session start, so a new session (or restart) may be
     needed before they can be invoked by name.

7. **Feature-branch workflow** (D-033, supersedes D-032). The user doesn't want direct pushes to `main`;
   everything goes through feature branch → PR → review → merge.
   - Checked GitHub enforcement: branch protection and rulesets → **HTTP 403** (private repo on the free
     plan). Added a committed **`.githooks/pre-push`** hook that rejects pushes to `refs/heads/main`,
     and **`.gitattributes`** (`* text=auto`, LF for hooks).
   - Tested the hook directly: push to main → exit 1 with a message; push of a feature branch → exit 0;
     mixed push including main → exit 1. `git add --renormalize .` changed no existing files.
   - Updated `docs/SUBAGENTS.md` (branches, PRs, merging; step 10; small changes still need a PR),
     `CLAUDE.md`, and the README ("Contributing workflow").
   - **This change is the first one delivered as a PR** (branch `chore/feature-branch-workflow`). The
     first four commits went straight to `main` before the rule existed.

8. **PR #39 merged** by the user (merge commit `9dfda75`; the user deleted the branch).
   - Synced local `main` (`git pull --ff-only --prune`) and deleted the local branch.
   - **Activated the hook** in this clone with `git config core.hooksPath .githooks`.
   - **Verified it:** the first dry-run push to `main` showed nothing, because `main` was up to date,
     so git had nothing to push and skipped the hook. Retried with a dangling probe commit
     (`git commit-tree`, on no branch) and dry-ran `push origin <probe>:refs/heads/main` → **rejected
     by pre-push**. A dry-run push to a new feature branch name was allowed. `main` was unchanged.
   - **Repo merge settings** (the user approved): merge commits only (squash and rebase disabled),
     auto-delete head branches on merge. Confirmed by the API response.
   - The user asked why the branch was called `chore/`: it's the Conventional Commits type for
     tooling and process changes that don't alter app behaviour. Offered to switch to another scheme;
     no change requested so far.
   - This log entry is itself delivered through a PR (`docs/merge-settings-and-hook`).

9. **PR #40 merged.** Branch auto-deleted by GitHub (the first real test of the setting).

### Open items
- User review of `docs/SUBAGENTS.md` and the agent definitions.

---

## 2026-11-17 — Session 3: Playwright E2E harness (secondary session)

**Context:** a second Claude Code session that runs alongside the main one and acts as orchestrator
for its own branches. The user asked for the first steps toward a basic, usable order screen, with
Playwright for UI tests.

### Decisions
- **D-034:** Playwright E2E for UI behaviour; Vitest stays for unit tests.
- **D-035:** order of work: #41 (harness) → #25 (start screen and draft store) → #8 → #26.
- **D-036:** this session works in its own worktree, `.claude/worktrees/41-playwright-e2e`, because the
  main session uses the shared checkout. Found while syncing: the main session had already merged #39
  and #40, switched the shared checkout to `main`, and deleted the old branch.

### Done
1. **Story selection:** read #25, #26, #27, #36, #8 and #14. #25 is the only UI story with no backend
   or M0 dependency. The user chose #25 first, Playwright E2E alongside Vitest, and a separate chore PR
   for the harness.
2. **Issue #41**, "Playwright E2E test harness for the order builder UI" (P1-should, area:testing,
   area:frontend, M4). Added to the board as In Progress, with Priority P1.
3. **Branch `chore/41-playwright-e2e`** in a worktree from `origin/main` (`990172d`). Unset its
   upstream, which `git worktree add` had set to `origin/main`.
4. **Harness:** the small-change path (no planner or reviewer subagents), because this is tooling with
   no app behaviour. It still goes through a PR.
   - `npm ci`, then `npm install -D -E @playwright/test@1.65.1` (latest, released 2026-11-13), then
     `npx playwright install chromium`.
   - `client/playwright.config.ts`: testDir `test/e2e`, Chromium project, its own `quasar dev
     --port 9100` through `webServer` (D-034 explains the dedicated port), and CI-aware retries,
     workers and `forbidOnly`.
   - `client/test/e2e/smoke.spec.ts`: the app shell shows the title in the header; an unknown deep link
     shows the 404 page, and Go Home returns to `/`.
   - Scripts `test:e2e` and `test:e2e:ui`. Playwright output is git-ignored and in `.dockerignore`.
     README test instructions updated.

### Verification
- `npm run test:e2e` → **2 passed** (5.5 s, starting the dev server itself on :9100).
- **Negative check:** temporarily changed the 404 text the spec expects → **1 failed** (`element(s)
  not found`), then reverted. So the spec really checks the page.
- `npm test` (Vitest) → 1/1. `npm run typecheck` → clean (vue-tsc also covers the new `.ts` files).
  ESLint on `src`, `playwright.config.ts` and `test/e2e` → clean. `prettier --check` on the new and
  changed files → clean. `npx quasar build` → OK.

### Problems found (not fixed here: one concern per branch)
- **`lint:check` can never fail on formatting.** It runs `prettier "<glob>"` without `--check`, which
  only prints the formatted files and exits 0. With `--check`, 25 existing files "fail", and every one
  of them is **CRLF line endings** from the Windows checkout (`* text=auto`), since Prettier defaults to
  `endOfLine: lf`. With `--end-of-line auto` they all pass. A fix would add `--check` and either set
  `endOfLine: auto` or force LF for `client/` in `.gitattributes`. Proposed as a follow-up `fix/` branch.

### Open items
- PR for #41: the user reviews and merges.
- Then #25: story branch, plan file, `pizza-planner` → `pizza-plan-reviewer` → Gate 1.
- Follow-up: the `lint:check` / line-endings fix above.

### #25 planning (after PR #42 was opened)
5. **PR #42** opened (`chore/41-playwright-e2e` → `main`, closes #41). There are no CI checks yet (#38).
6. **#25 set up:** branch `story/25-kiosk-start-screen`, **stacked on `chore/41-playwright-e2e`**, so its
   Playwright specs have the harness. It's in the same worktree, because the sandbox wouldn't create a
   second worktree from inside the first. Board → In Progress. Plan file from the template.
7. **pizza-planner** wrote section 1:
   - `/` start page and `/order` builder (`meta.building`), plus a pure, unit-tested `kioskRedirect` guard.
   - A Pinia setup store holding menu codes only: `{ lines[{id,sizeCode,toppingCodes,quantity}],
     promoCode, tip{mode,value:string} }`, with no prices.
   - Hand-rolled, versioned `sessionStorage` persistence that fails safe.
   - Cancel order in the header, confirmed through the Quasar Dialog plugin.
   - 12 Vitest + 10 Playwright tests. AC 4 (idle timeout) proposed as deferred.
8. **pizza-plan-reviewer, round 1: REVISE** (2 Majors, 9 Minors, in section 2):
   - **M1:** the plan was written against an out-of-date M0 status. My brief said #1–#7 were
     unanswered, but the main session had decided and closed them in the meantime. The AC 4 deferral
     has to be weighed again now that the full kiosk is in scope, because without a timeout a
     walked-away draft stays on screen for the next customer.
   - **M2:** promo cardinality. **Resolved by the orchestrator:** the #7 decision says "one code per
     order", so `promoCode: string | null` stands; the planner should cite it.
   - **Verified by the reviewer** (so the plan's first risk can be dropped): Quasar installs Pinia
     before it creates the router, and the first navigation happens later, so the guard can read the
     store.
9. **Found: a decision-number clash with the main session.**
   - PR #42 adds D-034 to D-036.
   - PR #43 (`docs/m0-decisions`) records the M0 answers as D-034 to D-040, but its closing comments
     on #1–#7, and #25's body, cite **D-037 to D-043**.
   - Both PRs also add a WORKLOG "Session 3".
   - The user is coordinating the fix with the main session.
10. **Handed off:** the user is having the main session take over this work. This session stopped
    here.

### Open items (for the main session)
- Fix the D-number and Session-heading clash between PRs #42 and #43. Whichever merges second renames
  its Session heading.
- The user decides: AC 4 idle timeout (include in #25, or a follow-up issue), and the planner's defaults
  (Cancel always confirms; `/` → `/order` while a draft is open).
- Then planner round 2 on #25 (address M1, M2 and the Minors in section 2, using the accepted M0
  decisions), a plan review, and Gate 1.
- After #42 merges: merge `main` into `story/25-kiosk-start-screen`.
- Remove the worktree `.claude/worktrees/41-playwright-e2e` (it has its own `node_modules`) once it's no
  longer needed. `.claude/worktrees/` is excluded in `.git/info/exclude`.
- Follow-up: the `lint:check` / CRLF fix.
---

## 2026-11-17 — Session 4: M0 decisions (main session)

**Context:** The user is running a **second chat** for the UI and Playwright tests. This chat takes the
backend side: decisions first, then the pricing core (M1) and the order service (M2). The two chats
should coordinate on the API contract and must not share a working tree (use a separate worktree).

### Decisions taken (D-037 to D-043)
| Question | Answer | Matches recommendation? |
|---|---|---|
| #1 Scope | **Full kiosk vision** (#25, #29, #33 → P1) | No: recommended P0 → P1 → P2-if-time |
| #2 Pricing math | Recommended rules after research; **tip % on the pre-tax total**, configurable | Mostly. The tip base changed from post-tax to pre-tax |
| #3 Pricing location | Server only, quote API, 409 on a mismatch | Yes |
| #4 Cancel | **Delete the order** | No: recommended a `Cancelled` status |
| #5 Payment | **Simulated payment in scope** | No: recommended deferring it as P2 |
| #6 Kitchen | Endpoint + optional timer | Yes |
| #7 Validation | Recommended limits; reject an ineligible SAVE5 | Yes |

### Research for #2 (the user asked how modern systems do it)
- **Tip base:** Toast defaults to calculating tips **before tax** (it's configurable), and reportedly on
  the pre-discount total. Square lets the merchant choose before or after tax, and its suggested tips
  are calculated after discounts. So there's no single standard.
  - The user asked whether "percentage of the total bill" really means after tax. Answer: it's
    ambiguous. The brief qualifies every pre-tax amount with "before tax" and uses "bill" only here,
    which leans post-tax, but industry practice leans pre-tax.
  - The user chose **pre-tax, configurable**.
- **Tax on discounts:** store-funded discounts reduce the taxable amount; manufacturer-reimbursed ones
  don't. Rules vary by state. → Tax applies after the promo discount.
- **Tips and sales tax:** voluntary tips generally aren't taxable; mandatory service charges are.
  → The tip isn't taxed.
- **Rounding:** the half-cent rule rounds up. States require invoice-level or line-level rounding, or
  allow either; Texas and Florida require invoice-level. → Round half-up, and round tax once per order.
  **.NET's `Math.Round` defaults to banker's rounding**, so `AwayFromZero` must be explicit. This is a
  candidate for the README's "verified myself" AI note.
- Sources:
  [Toast tip before/after tax](https://support.toasttab.com/en/article/Calculate-tip-before-or-after-taxes),
  [Toast community on discounts](https://community.toasttab.com/t5/restaurant-operations/how-can-i-make-toast-adjust-the-tip-amount-based-on-the-comped/m-p/16105),
  [Square tips and tax](https://community.squareup.com/t5/Questions-How-To/Does-Square-calculate-tips-before-or-after-sales-tax/m-p/21890),
  [Square tips after discounts](https://community.squareup.com/t5/Feature-Requests/Tips-Suggestions-based-on-total-bill-not-post-discounts/idc-p/799250/highlight/true),
  [TaxJar discounts](https://www.taxjar.com/blog/calculations/2021-12-sales-tax-discounts-coupons-promotions),
  [Minnesota RN 05-08](https://www.revenue.state.mn.us/sites/default/files/2011-11/RN_05-08.pdf),
  [Sovos rounding](https://sovos.com/blog/sut/rounding-vs-the-sales-tax-bracket-system/),
  [NY gratuities](https://www.tax.ny.gov/pubs_and_bulls/tg_bulletins/st/gratuities.htm),
  [Microsoft Math.Round](https://learn.microsoft.com/el-gr/dotnet/api/system.math.round?view=net-5.0).

### GitHub updates
- Closed #1–#7, each with a comment linking its decision.
- Updated the stories these decisions change. #19 (cancel → delete) and #20 (state machine) were
  rewritten. A "Decision updates (M0)" section was added to #9, #10, #12, #14, #15, #16, #21, #25,
  #29, #31 and #33.
- Relabelled #25, #29, #33 from `P2-stretch` → `P1-should`, removed `needs-decision` from #29, and
  synced the board's Priority field.
- **Board check:** the closed questions #1–#7 moved to **Done** automatically. #25 was already **In
  Progress**, set by the UI chat, not this one.

### Collision with the UI session (PR #42) and renumbering
- PR #42 (`chore/41-playwright-e2e`, from the UI session) had already claimed **D-034 to D-036** and a
  "Session 3" heading. This session's decisions were renumbered **D-034…D-040 → D-037…D-043**, with a
  single-pass regex so nothing was replaced twice. This section became "Session 4".
- The same renumbering was applied on GitHub: the decision comments on #1–#7 (edited in place) and the
  13 annotated or rewritten story bodies. Spot-checked #4 and #19.
- **Ownership overlap:** PR #42's D-035 plans "#41 → #25 → **#8** → #26" in the UI session, while this
  session intended to start the backend with #8. The user needs to decide who owns #8.
- **Process lesson for parallel sessions:** decision IDs and session numbers are claimed when a PR
  merges, not when it's written. Whichever PR merges second renumbers its entries.

### Absorbing the UI session (the user's decision: one driver)
The user decided **this session is the single driver** and absorbs the UI session's work. That also
settles #8's ownership: the order from D-035 (#41 → #25 → #8 → #26) now runs here.
- **Handed over:** PR #42 (`chore/41-playwright-e2e`, closes #41), and branch
  `story/25-kiosk-start-screen` stacked on it (commit `623ea31`: #25 plan round 1, with the reviewer's
  round 1 REVISE, plus handoff notes). Everything was already pushed, so no code had to be copied.
- **Re-verified #42 independently** in its worktree: `vue-tsc` clean; ESLint (src, test, playwright
  config) clean; Vitest 1/1; **Playwright 2/2**. `prettier --check` fails on 25 files, which is the known
  CRLF line-ending issue, pre-existing on `main`.
- **#25 answers from the user:** the idle timeout (AC 4) becomes a **separate follow-up story, #44**
  (P1, M3, depends on #25). Both planner defaults are kept: Cancel order always confirms, and `/`
  redirects to `/order` while an order is open. Reviewer Major 2 (promo field) is settled by D-043:
  one promo code per order, so a single `promoCode` field.
- **#42/#43 conflicts resolved in advance:** merged `origin/chore/41-playwright-e2e` into
  `docs/m0-decisions` and kept both sides of each conflict, in number order. Decisions table D-034–D-036
  then D-037–D-043; their sections before the "M0 decisions" block; WORKLOG Session 3 then Session 4.
  **Merge order: #42 first, then #43.** #43 then shows only its own changes.

### #25 planning, continued here
- **#42 and #43 merged** by the user (`e583d06`, `68b27c7`). One hiccup: my first sync ran before #43 was
  merged, and I deleted the *local* `docs/m0-decisions` branch while its PR was still open. Nothing was
  lost (the remote branch and PR were intact), and the user then merged it.
- **Old worktree:** `git worktree remove` unregistered it and deleted its contents. An **empty folder**
  is still locked by another process (probably the absorbed session's shell). It's harmless and excluded
  from git, so it gets deleted once that session is closed.
- **Merged `main` into `story/25-kiosk-start-screen`** (`1393b65`). The WORKLOG conflict was resolved by
  keeping both: the Session 3 #25-planning notes, then Session 4.
- **Plan header updated** with the user's answers (#44 split; both defaults kept; D-043 one promo code;
  new decisions numbered D-044+).
- **Subagents:** the `pizza-*` agent types aren't registered in this session, because they were added
  after it started. So each role ran as a `general-purpose` agent on **opus**, briefed with the
  6-part template and told to follow `.claude/agents/<role>.md` as its contract.
  - **Planner round 2:** a complete, self-contained Round 2 with a "Response to review round 1" table.
    New: the `useEndOrder().endOrder()` single exit path, shared with #44; warn-once persistence; scoped,
    exact Playwright locators; E1's Cancel checks moved to step 3 so every step stays green; tests trimmed
    to 12 unit + 8 E2E (+1 optional).
  - Checked that it only changed section 1, then committed (`0149b08`).
  - **Plan review round 2: APPROVE.** All 11 round-1 findings were resolved, with no new Blocker or Major.
    The reviewer re-ran ESLint (exit 0) and checked the plan's claims against the installed Quasar,
    vue-router and Vitest. Four new Minors for the developer to fold in: `unbound-method` /
    `no-unsafe-*` traps in the tests, a ≥ 96 px assertion for Start order, filtering E8's console
    warning, and a complete `Storage` stub.
- **Proposed decisions at Gate 1:** D-044 (kiosk flow, route guard and shared exit path) and D-045
  (draft-order store and persistence), plus three README assumption lines.

### #25 delivered (Gate 1 → PR)
- **Gate 1:** approved by the user at 21:13 as part of the process review (D-046), and recorded D-044
  and D-045.
- **`pizza-developer` (sonnet, as general-purpose):** 4 step commits in about 6 minutes. Optional E8
  dropped.
- **`pizza-reviewer` (opus): PASS**, with the cause of E8 explained.
- **Orchestrator:** added the Enter-key check (and mutation-checked it), then re-ran every check: all
  green. Details in the plan's §5.
- **Cost of the full four-role flow for one P1 screen:** 4 opus runs plus 1 sonnet run, and an
  871-line plan. That's why D-046 slims the process for the remaining PRs.

### Open items
- After #25 merges: PR A (pricing core) is ready on `story/8-14-pricing-core` and in review.
- `fix/` branch: make `lint:check` really check formatting (CRLF / `endOfLine`).
- Publish the menu and quote API contract when #8 and #14 are planned.
- #29 payment sub-decisions (timing, reconciliation on update and delete) are settled at its plan gate.

---

## 2026-11-17 — Session 5: PR A, pricing core (#8–#14, #34)

**Context:** the user's process review (21:13). It approved #25 at Gate 1, set up D-046 (4 batched PRs,
slim process, cut list; supersedes D-030 and D-037), and asked for the API contract first. PR A was
built in its own worktree (`.claude/worktrees/pricing-core`, branch `story/8-14-pricing-core`) while the
#25 developer and reviewer subagents ran in the main checkout.

**Done (one commit per story):**
- **#9** `Money.RoundToCents`: half-up, explicit `AwayFromZero`. A test documents that `Math.Round(0.125m, 2)`
  gives 0.12.
- **#8** menu entities and `MenuSeed`, which is the single source for the EF seed and for the tests. The
  `InitialMenu` migration, an immutable case-insensitive `MenuSnapshot`, and `GET /api/menu` with per-size
  topping prices. **Verified against Postgres:** the migration applied on startup and the endpoint served
  the seeded data.
- **#11** promotion engine: `IPromotionRule` types in code, promo rows as data, conflicts through
  exclusion groups, the discount capped at the subtotal. A test adds SAVE10 and TUESDAY as data only.
- **#13** description builder. **#10/#12** `OrderPricer`, which implements the D-038 formula, the D-043
  limits with per-field messages, and both tip bases.
- **#14** `POST /api/orders/quote`: 400 ProblemDetails with `code`, `traceId` and field errors.
  `docs/API.md` (the full contract, including the order and kitchen endpoints for PR B) and `.http`
  examples.
- **#34** invariant tests: 500 seeded random orders × 2 tip bases. Every amount is whole cents, the parts
  sum to the total, the discount stays in [0, subtotal], tax is computed after the discount, and pricing
  is deterministic. Also line-order independence and the SAVE5 boundary.

**Verification:** `dotnet build` with 0 warnings; **`dotnet test` 70/70**.
- **Mutation check (done by hand, for the AI note):** changing the tax to apply before the discount made
  **5 tests fail**: both invariant runs, the worked example, tax-after-discount, and the HTTP quote test.
  Restored afterwards.
- **Found while testing:** a zero `decimal` literal serialized as `0`, not `0.00`. Fixed with `0.00m`, and
  a test asserts it.
- **Docker gotcha:** `docker compose up db` from a worktree starts a *new* compose project (named after the
  folder), and it clashed on :5432. Removed it and used the existing `pizzaorderdesigner-db-1`.

**Process docs (D-046):** D-030 and D-037 marked superseded. `CLAUDE.md` "Process" section rewritten, and
`SUBAGENTS.md` §0 "Current process" added (the four-role sections are kept as history).

### Open items
- PR B (order service): short plan first. PR C (UI). PR D (README).
- `fix/` branch for `lint:check` (CRLF): still open.

---

## 2026-11-17 — Session 6: PR B, order service (#15–#22, #35)

**Plan:** `docs/plans/pr-b-order-service.md` (about 95 lines, the one planning pass D-046 allows). Built
in its own worktree (`.claude/worktrees/order-service`), stacked on PR A.

**Done (commits by story):**
- **#15** orders, lines and idempotency records. The `AddOrders` migration uses `jsonb` for the priced
  snapshot, `text[]` for toppings, a cascade delete, and `xmin` as the row version. Npgsql correctly
  doesn't create an `xmin` column (checked in the generated SQL).
- **#20** the `OrderStatusFlow` transition table. **#22** `OrderProblems` (stable codes and customer
  wording) plus a friendly 500 handler, tested so no exception text or stack frames leak.
- **#16–#19, #21** `OrderService` and its endpoints (D-047). Deviation: the kitchen advance returns 409
  `order.status_changed` instead of retrying. The optional auto-advance timer is on in docker-compose.
- **#35** Testcontainers integration tests (real Postgres): retries, 10 concurrent duplicates, key
  reuse, a late retry after a cancel, a price mismatch, the ETag rules, cancel rules, and linear advance.

**A finding worth keeping for the AI note:** the parallel-HTTP race test passed, but logging showed
**cancel won 25/25 rounds**, so it never exercised "kitchen wins". A mutation (an unconditional DELETE)
slipped past it. Fixed by adding **deterministic race tests** that hold one side's row lock in a
transaction, prove the other side waits, then commit and assert it loses cleanly (both orderings).
Mutation checks:
- an unconditional DELETE now fails 2 tests,
- removing the unique-violation replay fails the concurrency test,
- **a known gap:** removing the status check from the advance `WHERE` isn't caught. It only guards a
  stale advance moving an order backwards, and forcing that interleaving needs hooks in the service.
  Documented rather than tested.

**Verification:** `dotnet test` **96/96** (about 8 s including the container).
**Host smoke test against the dev DB:**
- submit → 201 $31.23 with an ETag,
- retry → 200 with the same id,
- advance → Started,
- cancel → 409 `order.already_started` with a traceId.

### Open items
- One reviewer pass on PR B. PR C (UI) and PR D (README).
- **Review (opus): CHANGES REQUESTED.**
  - The Major: an update losing to a cancel returned 500, from an FK violation on the new lines.
  - Seven Minors.
  - **All fixed:** a hooked-provider interleaving test for both update races, and a deterministic
    stale-advance test (**closing the known gap**). Also status-before-version, ProblemDetails for bare
    404s and JSON errors, a key-length message, the price.changed "nothing stored" assertion, 0 build
    warnings, and the PUT-retry 412 documented.
  - **100/100**. Both new tests were mutation-checked.

## 2026-11-17 — Session 7: PR C, order builder UI (#26–#28, #30–#32)

**Built by `pizza-developer` (sonnet)** in its own worktree (`.claude/worktrees/order-ui`), one commit per
story: #32 API client and friendly errors, #26 menu and customizer, #27 debounced quote and description,
#28 tip and promo, #30 retry-safe submit, #31 status page with polling and cancel. **Cut:** editing a
placed order (#31's PUT/If-Match part), so #31 stays open (`Refs`, not `Closes`). Notes are in
`docs/plans/pr-c-order-builder-ui-notes.md`.

**Verified against the real API** by the orchestrator (PR B's API + this UI, real Postgres):
- the menu loads from the DB,
- the running total is server-computed: $28.56 subtotal, $1.86 tax,
- ILOVEPIZZA + 15% → **$31.23** (the contract example),
- the order is placed and the status page shows it,
- after a kitchen advance it shows "Preparing" and Cancel is hidden.

**Review (opus): CHANGES REQUESTED, with 2 Majors that could each create a duplicate or unwanted order.**
- An edit after an unclear failure minted a new key.
- Cancel was available during an in-flight submit.

**Fixed by the orchestrator (D-048):** the order locks while a submit is in flight or its outcome is
unclear, only Retry remains (same body and key), and the pending key and body survive a refresh. Also:
an unreadable 2xx is treated as unclear, messages show title + detail, and the mock was aligned with the
real API, including a new "server stored it, response lost" mode.

**Mutation check:** forgetting the key after an unclear failure fails 2 specs.

**Checks:** typecheck, ESLint and Prettier ✅; Vitest **42/42**; Playwright **29/29**.

---

## 2026-11-17 — Session 8: PR D, README deliverables (#37)

The README was rewritten to answer every deliverable in the brief:
- run instructions (Docker and local), and the three test commands,
- the API summary with curl examples (full contract in `docs/API.md`),
- the **Assumptions** list (pricing, validation, lifecycle, kiosk),
- the **design write-up** (money and where pricing runs; extensibility; idempotent retries and the
  cancel-vs-start race),
- the invariants tested, with the mutation-check results, and what isn't covered,
- the cut list,
- the **AI usage note**,
- an honest time-spent account.

**Verified before writing "it runs":** rebuilt the full compose stack from the final code (PRs A, B and C)
and drove it through nginx on :8080:
- health and the SPA deep link,
- the menu,
- quote $31.23,
- submit → 201 with an ETag,
- retry with the same key → 200 with the same id,
- the auto-advance timer → Started after about 45 s,
- cancel after start → 409.

A first scripted attempt sent malformed JSON (my PowerShell `TrimEnd('}')` stripped every closing brace).
The server correctly answered 400. Fixed in the script, not the app.

---

## 2026-11-17 — Session 9: fix #48 (Location header behind nginx)

**Bug (found by the user in Docker):** `POST /api/orders` through nginx returned
`Location: http://localhost/api/orders/{id}`, without `:8080`, so following it failed. The cause was
`proxy_set_header Host $host;` in `client/nginx.conf`: `$host` drops the port, and ASP.NET builds the
`CreatedAtAction` URL from the Host header.

**Fix:** `proxy_set_header Host $http_host;`, which keeps the port the client used.

**Regression check:** the new `scripts/smoke-compose.sh` tests the running compose stack through nginx:
health, menu, the $31.23 quote, a submit **whose Location is followed**, and a same-key retry.
- **Before the fix**, against the old web container, it failed on #48.
- **After rebuilding** the web container, it passes.

The Playwright and backend suites never go through nginx, which is why they couldn't catch this. The
README's Tests section now lists the script.

---

## 2026-11-17 — Session 10: final review handover and fixes

**Handover from a secondary review session** (it reviewed `main` at `0130e42` on a clean export):
- 100/100 backend tests; Vitest 42/42; Playwright 29/29; `vue-tsc` clean; `quasar build` OK.
- Requirements 1–6 all work, and every README deliverable is present. The README's claims were
  spot-checked: $31.23, the test names, port 5158, D-048.
- **Open items it raised:**
  - #48's `.http` acceptance criterion was missed. PR #51 closed the issue without it.
  - **The AI usage note claimed, in the first person, checks that Claude performed.** It's an honesty
    issue in a section the brief reads closely.
  - The repo is private (graders need access).
  - Optional: #31's edit UI, the `lint:check` fix, closing the cut-list issues, and confirming the time
    line.
- **Incident it reported:** after its worktree was removed, the empty folder
  `.claude/worktrees/41-playwright-e2e` stayed behind. Running `git switch --detach origin/main` from
  inside it climbed to the main checkout and detached it. The other session switched it straight back to
  `main` at `93b2479`. The tree was clean and nothing was lost. Its review was read-only after that.
  - **Lesson:** delete an abandoned worktree folder before anyone runs git from inside it. The folder is
    still locked by a process here and gets deleted once that session is closed.

**Decisions by the user:**
- **The AI note:** the user runs two checks personally (places the $31.23 order in the UI; breaks
  tax-after-discount in `OrderPricer` and watches the tests fail). The note then attributes each
  verification precisely: what the user did, and what Claude or the subagents did.
- **Time:** the user started at about 20:18, so the README's "19:20" was wrong and was corrected to
  the user's numbers. That's about 2 h 05 m at 22:24, so the work fits the 3-hour box.

**Done:**
- The `.http` file now documents all three targets (Docker via nginx :8080, the API container :5080,
  local dev :5158). It also has examples for submit (with a fixed Idempotency-Key, to show a safe
  retry), get, update with `If-Match`, kitchen advance and cancel. The update example's
  `expectedTotal` (17.21) was checked against the live quote endpoint.
