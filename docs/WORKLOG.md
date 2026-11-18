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

### Open items
- **User review of the backlog**, and answers to the M0 questions (#1–#7), before any feature work.
- User review of `docs/SUBAGENTS.md` and the agent definitions.
- Merge the `chore/feature-branch-workflow` PR. Then activate the hook in this clone
  (`git config core.hooksPath .githooks`) and decide the repo merge settings (merge commits only? delete
  branch on merge?).
