# Decision Log

Every meaningful technical or process decision is recorded here: what was decided, why, and what was
considered instead. Newest entries go at the bottom. Status is one of **Accepted**, **Superseded by D-xxx**,
or **Open** (decided provisionally, revisit later).

| ID | Date | Decision | Status |
|----|------|----------|--------|
| D-001 | 2026-11-17 | Stack: C# .NET 10 Web API, Vue.js frontend, EF Core → PostgreSQL with migrations | Accepted |
| D-002 | 2026-11-17 | Monorepo layout: `server/`, `client/`, `docs/`, `docker-compose.yml` at root | Accepted |
| D-003 | 2026-11-17 | Three containers: `db`, `api`, `web` | Accepted |
| D-004 | 2026-11-17 | Local dev runs API + Vite on the host against the Postgres container | Accepted |
| D-005 | 2026-11-17 | Pin .NET SDK 10.0.404 via `global.json` | Accepted |
| D-006 | 2026-11-17 | Solution split: Api / Data / Api.Tests, `.slnx` format | Accepted |
| D-007 | 2026-11-17 | Controllers rather than Minimal APIs | Accepted |
| D-008 | 2026-11-17 | EF Core 10.0.15 + Npgsql 10.0.3, local `dotnet-ef` tool | Accepted |
| D-009 | 2026-11-17 | Migrations live in the Data project | Accepted |
| D-010 | 2026-11-17 | Apply migrations on startup, behind a config flag | Open |
| D-011 | 2026-11-17 | Same-origin `/api` via proxies, so no CORS | Accepted |
| D-012 | 2026-11-17 | PostgreSQL 17 (alpine) image | Accepted |
| D-013 | 2026-11-17 | Vue 3 + Vite + TypeScript, Router, Pinia, Vitest, ESLint/oxlint, Prettier | Superseded by D-020 |
| D-014 | 2026-11-17 | Removed HTTPS redirection from the API | Accepted |
| D-015 | 2026-11-17 | `/api/health` endpoint with a database check | Accepted |
| D-016 | 2026-11-17 | Dev-only DB credentials checked in, overridable via `.env` | Accepted |
| D-017 | 2026-11-17 | xUnit (template default, v2) for backend tests | Open |
| D-018 | 2026-11-17 | Disable Npgsql GSS encryption in the container connection string | Accepted |
| D-019 | 2026-11-17 | Claude Code session rooted at `PizzaOrderDesigner/`, not the parent workspace | Accepted |
| D-020 | 2026-11-17 | Frontend: latest Quasar (CLI, `@quasar/app-vite`) + latest stable Vue | Accepted |
| D-021 | 2026-11-17 | Host Node 24 (24.22.1) + global npm 12 | Accepted |
| D-022 | 2026-11-17 | ESLint (+ Prettier) as the frontend linter, not oxlint | Accepted |
| D-023 | 2026-11-17 | Vitest via Quasar's official testing extension | Accepted |
| D-024 | 2026-11-17 | Vue Router in `history` mode | Accepted |
| D-025 | 2026-11-17 | Strip the Quasar template's demo content | Accepted |
| D-026 | 2026-11-17 | Assignment kept in-repo as `docs/ASSIGNMENT.md` | Accepted |
| D-027 | 2026-11-17 | Private GitHub repo `pizza-order-designer`, branch `main` | Accepted |
| D-028 | 2026-11-17 | Work tracked as GitHub issues + Project board (labels, milestones M0–M4) | Accepted |
| D-029 | 2026-11-17 | Open questions are `needs-decision` issues with a recommendation; resolved answers become README assumptions | Accepted |
| D-030 | 2026-11-17 | Four-role subagent workflow (planner → plan reviewer → developer → reviewer) with two user gates | Superseded by D-046 |
| D-031 | 2026-11-17 | One plan file per story in `docs/plans/`, the single artifact every role writes to | Accepted |
| D-032 | 2026-11-17 | Branch per story (`story/<N>-<slug>`), PR with `Closes #N`, user merges | Superseded by D-033 |
| D-033 | 2026-11-17 | No direct pushes to `main`: every change goes through a feature branch + PR the user merges, enforced by a pre-push hook | Accepted |
| D-034 | 2026-11-17 | Playwright E2E for UI behaviour; Vitest stays for unit tests | Accepted |
| D-035 | 2026-11-17 | First UI work: Playwright harness (#41), then #25, then #8 → #26 | Accepted |
| D-036 | 2026-11-17 | A secondary session works in its own git worktree under `.claude/worktrees/` | Accepted |
| D-037 | 2026-11-17 | Scope: full kiosk vision (start screen, simulated payment, kitchen view) in scope; required path still built first (#1) | Superseded by D-046 |
| D-038 | 2026-11-17 | Pricing math: tax after discount, tip untaxed, round half-up, tax rounded once per order, tip % on pre-tax total (configurable) (#2) | Accepted |
| D-039 | 2026-11-17 | Pricing runs on the server only; the UI shows server quotes; submit re-prices and rejects a mismatch (#3) | Accepted |
| D-040 | 2026-11-17 | Cancel deletes the order; idempotency records outlive orders; status flow is linear (#4) | Accepted |
| D-041 | 2026-11-17 | A simulated payment step is in scope (no real payments) (#5) | Accepted |
| D-042 | 2026-11-17 | Kitchen: an advance endpoint plus an optional auto-advance timer (#6) | Accepted |
| D-043 | 2026-11-17 | Validation limits; an ineligible SAVE5 is rejected with a message (#7) | Accepted |
| D-044 | 2026-11-17 | Kiosk flow: `/` start, `/order` builder, route guard, one shared exit path (#25) | Accepted |
| D-045 | 2026-11-17 | Draft order in a Pinia store, persisted to sessionStorage (versioned, fail-safe) (#25) | Accepted |
| D-046 | 2026-11-17 | Timebox reset: 4 batched PRs, slim process, scope cut list. Supersedes D-030, D-037 | Accepted |
| D-047 | 2026-11-17 | Lifecycle guarantees: idempotency table + transaction, conditional SQL for cancel/advance, xmin for update | Accepted |

---

### D-001 — Stack
**Decision:** C# .NET 10 Web API, Vue.js SPA, EF Core with the Npgsql provider against PostgreSQL, schema
managed through EF Core migrations.
**Why:** The candidate's day-to-day stack, so time goes into the problem, not the tooling.

### D-002 — Monorepo layout
**Decision:** One repository: `server/` (the .NET solution), `client/` (the Vue app), `docs/` (these logs),
and `docker-compose.yml` at the root.
**Why:** A reviewer clones one thing and runs one command. Each side keeps its own `.gitignore`,
`Dockerfile` and tooling, so it can be understood on its own.

### D-003 — Three containers
**Decision:** `db` (postgres), `api` (ASP.NET Core), `web` (nginx serving the built SPA and proxying `/api`).
**Why:** `docker compose up --build` runs the whole system with no host prerequisites beyond Docker. Each
tier depends on the next only through configuration (a connection string, an upstream URL).
**Alternatives:** Two containers, with the API serving the built SPA as static files: fewer parts, but it
couples frontend deploys to the API. Decided by the user.

### D-004 — Dev workflow
**Decision:** For day-to-day work, run `docker compose up -d db`, then `dotnet watch` and `npm run dev` on
the host. The full container stack is for "run it like a reviewer would".
**Why:** Hot reload, debugger attach and IDE tooling all work best on the host. The host also needs the
SDK for `dotnet ef migrations add`.

### D-005 — .NET SDK pinned via `global.json`
**Decision:** `global.json` pins SDK `10.0.404` with `rollForward: latestFeature`.
**Why:** The machine also has the .NET 8 SDK. Pinning guarantees the 10.x SDK is used and documents the
requirement. `latestFeature` lets newer 10.0.x patch/feature bands work without editing the file.
**Note:** The .NET 10 SDK was installed with `winget install Microsoft.DotNet.SDK.10`.

### D-006 — Solution structure
**Decision:** `PizzaOrderDesigner.Api` (web host, controllers), `PizzaOrderDesigner.Data` (DbContext,
entity configurations, migrations), `PizzaOrderDesigner.Api.Tests`. The solution uses the new `.slnx`
format, the .NET 10 default.
**Why:** Keeps persistence concerns out of the web project without the ceremony of a full
Clean-Architecture split for a take-home. A Domain/Application project can be extracted later if the
business logic grows enough to justify it.

### D-007 — Controllers over Minimal APIs
**Decision:** Use `[ApiController]` controllers (`dotnet new webapi --use-controllers`).
**Why:** A familiar, conventional structure that groups endpoints by resource and is easy for reviewers to
navigate. Easy to revisit while the API is still empty.

### D-008 — EF Core / Npgsql versions
**Decision:** `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, with
`Microsoft.EntityFrameworkCore.Relational`, `.Design` and
`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` all pinned to 10.0.15. `dotnet-ef`
10.0.15 is installed as a **local tool** (`server/dotnet-tools.json`).
**Why:** EF Core 11 is the latest release but only targets `net11.0`, so `dotnet add package` without a
version failed. Npgsql 10.0.3 transitively brings EF Core 10.0.4. Pinning Relational to 10.0.15 keeps
every project on the same EF version as the Design package. The local tool manifest means
`dotnet tool restore` gives anyone the matching `dotnet-ef`.

### D-009 — Migrations in the Data project
**Decision:** The migrations assembly is `PizzaOrderDesigner.Data`. The API is the startup project.
**Command:** run from `server/`:
`dotnet ef migrations add <Name> -p src/PizzaOrderDesigner.Data -s src/PizzaOrderDesigner.Api`
**Why:** The schema history lives next to the model it describes.

### D-010 — Migrate on startup (Open)
**Decision:** If `Database:ApplyMigrationsOnStartup` is `true`, the API calls `Database.MigrateAsync()`
at startup. It is `false` in `appsettings.json`, and `true` in `appsettings.Development.json` and in
docker-compose.
**Why:** The simplest way for a reviewer's `docker compose up` to end with a ready schema.
**Trade-off:** In production, a dedicated migration step (EF migrations bundle or a generated SQL script
run by the pipeline) is safer: no race between multiple instances, and no DDL rights for the app user.
Worth mentioning in the write-up.

### D-011 — Same-origin API, no CORS
**Decision:** The browser always calls relative `/api/...`. In containers, nginx proxies `/api/` to
`http://api:8080`. In dev, the dev server's proxy forwards `/api` to `http://localhost:5158`.
*Updated by D-020:* the dev proxy now lives in `quasar.config.ts` → `devServer.proxy`, and the override
env var is `API_PROXY_TARGET`.
**Why:** No CORS configuration, and identical frontend code in both modes.

### D-012 — PostgreSQL 17
**Decision:** `postgres:17-alpine`, with data in the named volume `pgdata`.
**Why:** Mature and fully supported by Npgsql 10. PostgreSQL 18 changed the image's data-directory/volume
layout, and nothing in this project needs 18 features. Easy to bump.

### D-013 — Frontend scaffold (Superseded by D-020)
**Decision:** `create-vue` 3.24 with `--ts --router --pinia --vitest --eslint --prettier --bare`. This
gives Vue 3.5, Vite 8, TypeScript 6, Vue Router 5, Pinia 4, Vitest 4, ESLint 10 with oxlint, and Prettier.
**Why:** The official scaffold. TypeScript catches API-contract mistakes early. Router and Pinia are
likely needed for a multi-step pizza designer and order flow. `--bare` skips the demo components.

### D-014 — No HTTPS redirection in the API
**Decision:** Removed `app.UseHttpsRedirection()` from the template.
**Why:** The API runs plain HTTP behind nginx (container) or the Vite proxy (dev). TLS would terminate at
the edge in a real deployment. Leaving the redirect in produces warnings and broken redirects inside
the container network.

### D-015 — Health endpoint
**Decision:** `GET /api/health` uses ASP.NET Core health checks, including an EF Core `DbContext` check.
**Why:** It proves the API → DB path end to end, which helps docker-compose smoke tests and reviewers.

### D-016 — Dev credentials in source
**Decision:** `appsettings.Development.json` and the docker-compose defaults use `pizza` / `pizza_dev`.
Compose reads `POSTGRES_DB/USER/PASSWORD` from an optional root `.env` (see `.env.example`), which
is git-ignored.
**Why:** Zero-config local start. These credentials are for a local throwaway database only, never a real
secret.

### D-017 — Test framework (Open)
**Decision:** Kept the `dotnet new xunit` template default (xUnit 2.9.3) with one placeholder test.
**Revisit:** Moving to xUnit v3, and adding `Microsoft.AspNetCore.Mvc.Testing` +
Testcontainers-PostgreSQL integration tests once the first endpoints exist.

### D-018 — `Gss Encryption Mode=Disable` for the containerized API
**Decision:** Append `Gss Encryption Mode=Disable` to `ConnectionStrings__PizzaDb` in docker-compose.
**Why:** Npgsql 9+ tries GSS (Kerberos) encryption first by default. The `aspnet:10.0` image has no
`libgssapi_krb5.so.2`, so every connection logged `Cannot load library libgssapi_krb5.so.2`. We use
password auth on a private network, so GSS is not needed.
**Alternatives:** Installing `libgssapi-krb5-2` in the runtime image adds size for a feature we don't
use. The host dev connection string is unchanged, because Windows uses SSPI and doesn't hit this.

### D-019 — Session rooted at the project folder
**Decision:** Moved the AI-assistant session's working directory from `C:\Users\Staxil\Claude Workspace`
to `PizzaOrderDesigner/`.
**Why:** The project's `CLAUDE.md` (tracking rules, conventions) then loads in every session, and
subagents and tooling start in the project, not a parent folder that holds unrelated material. It also
matches where the git repo will be rooted. Nothing on disk moved.

### D-020 — Quasar CLI frontend (supersedes D-013)
**Decision:** The user asked for the latest Quasar and Vue. `client/` was re-scaffolded with
`create-quasar` 5.0.38 (`--template app`, presets `typescript sass eslint pinia`, `--no-git`). This gives
Quasar 2.35.5, `@quasar/app-vite` 3.10.4 (Vite 8.3.8), Vue 3.5.45, Vue Router 5.5.1, Pinia 4.1.0,
TypeScript 6.0.3, and `@quasar/extras` (Roboto + Material Icons).
**Why the CLI over `@quasar/vite-plugin`:** It's Quasar's primary, fully featured setup (boot files,
`quasar.config.ts`, layouts, auto-import, and the setup most docs and examples assume), and it's
already on current Vite. The old client was an empty scaffold, so re-scaffolding cost nothing. Chosen
by the user.
**"Latest Vue":** 3.5.45 is npm's `latest` tag. 3.6 is RC only, so it wasn't used.
**Carried over:** `Dockerfile` (now `npm ci --ignore-scripts` → `quasar build` → `dist/spa` served by
nginx), `nginx.conf`, `.dockerignore`, and the `/api` dev proxy (port 9000, `open: false`).
**Trade-off:** Unit tests need Quasar's testing extension (D-023) instead of plain Vitest wiring.

### D-021 — Host Node 24 + npm 12
**Decision:** Upgraded host Node from 22.16 to **24.22.1** (`winget install OpenJS.NodeJS.LTS --version
24.22.1`, upgraded in place), and the global npm from 11.4.1 to **12.2.3**.
**Why:** `@quasar/app-vite` requires Node `^22.22 || ^24 || ^26...`. Node 24 matches the `node:24-alpine`
Docker build image. npm 11.4.1 (installed globally under `%APPDATA%\npm`, which shadows Node's bundled
npm) had the `edgesOut` install bug.
**Note:** Node **26** became the active LTS in Oct 2026, and 24 is now maintenance LTS (supported until
April 2028). 24 was kept because the user chose it and Quasar supports both. Moving to 26 later would
also mean bumping the Docker image.
**Side effect:** npm 12 blocks dependency install scripts by default. It warned about
`@parcel/watcher` (an optional native file watcher), and the build is unaffected.

### D-022 — ESLint, not oxlint
**Decision:** Use the `eslint` preset, which includes Prettier via `@vue/eslint-config-prettier`.
**Why:** create-quasar's `eslint` and `oxlint` presets can't be combined. ESLint is the standard, has
mature Vue/TS rules, and is what `vite-plugin-checker` runs during `quasar dev/build`.
**Alternative:** oxlint is much faster, but has thinner Vue template support.

### D-023 — Vitest via `@quasar/testing-unit-vitest`
**Decision:** Use Quasar's official extension
(`@quasar/quasar-app-extension-testing-unit-vitest` 3.0.0) with Vitest 5.1, `@vitest/ui`,
`@vue/test-utils` 2.6, and happy-dom. Tests live in `client/test/vitest/__tests__/`. Scripts are
`test` / `test:unit:ci` (single run), `test:unit` (watch) and `test:unit:ui`.
**How:** `quasar ext add` only asks one interactive question (whether to add the UI), which can't be
answered from a non-interactive shell. So its install step was applied by hand: rendered the AE's
TypeScript template (`vitest.config.ts`, `test/vitest/setup-file.ts`), added its scripts, and
registered it in `quasar.extensions.json` with `ui: true`. Its index script only checks versions.
**Why:** `installQuasarPlugin()` and `quasarViteTestingConfig()` handle Quasar's component
registration and Vite config for tests, which would otherwise have to be hand-written.

### D-024 — History router mode
**Decision:** `build.vueRouterMode: 'history'` (the template default was `hash`).
**Why:** Clean URLs (`/order/42` instead of `/#/order/42`). nginx already falls back to `index.html`
(`try_files`), and Quasar emits absolute asset/icon URLs, so deep links work. Verified in the container.

### D-025 — Strip template demo content
**Decision:** Removed `EssentialLink.vue`, `SecondPage.vue`, `example-store.ts`, the Quasar logo, and the
unused `pnpm-workspace.yaml` (we use npm). `MainLayout` is now just a header ("Pizza Order Designer"),
and `IndexPage` is a placeholder. Kept `ErrorNotFound.vue`, the Pinia/router setup, and
`.vscode/mcp.json` (Quasar's MCP server for VS Code users).
**Why:** Same spirit as the original `--bare` scaffold. Real pages come with the assignment.

### D-026 — Assignment as Markdown in the repo
**Decision:** The take-home brief (`.docx`) was converted to `docs/ASSIGNMENT.md`. The content is
unchanged; bold pseudo-headings became real Markdown headings.
**How:** pandoc wasn't installed, so a small Python script read the document XML directly (paragraph
styles, numbering, bold/italic, tables). The result was checked against the source.
**Why:** It's easier to read, diff and link to from issues and docs. The brief says the candidate owns
the repository, so keeping the brief in it is fine.

### D-027 — Repository hosting
**Decision:** A **private** GitHub repo, `TheSpaghettiProgrammer/pizza-order-designer`, default branch
`main`, created with `gh repo create --private --source . --push`. The first commit is the project skeleton.
**Why:** The assignment requires a hosted git repo (no zip). The user asked for private. Commit history
is graded under *Judgment*, so commits should be small and meaningful from here on, referencing issues
(`Refs #N` / `Closes #N`).

### D-028 — Work tracking: issues + Project board
**Decision:** Every unit of work is a GitHub **issue** (not a draft item), so it can be linked from commits
and PRs and discussed. All issues sit on a GitHub **Project** board.
- **Labels:**
  - type: `story`, `question`, `needs-decision`
  - priority: `P0-required` (the assignment requires it), `P1-should` (scores on the evaluation
    criteria), `P2-stretch` (kiosk vision beyond the brief)
  - area: `area:pricing`, `area:order-service`, `area:frontend`, `area:observability`,
    `area:testing`, `area:docs`, `area:devops`
- **Milestones:** M0 Decisions → M1 Menu & pricing core → M2 Order service & lifecycle → M3 Order
  builder UI → M4 Quality & deliverables.
- **Story format:** a user story, acceptance-criteria checklist, notes/design, and `Depends on` links.

**Why:** The user wants everything tracked. The P0/P1/P2 split keeps the 3-hour timebox and the kiosk
vision visible side by side (see #1).
**Board:** https://github.com/users/TheSpaghettiProgrammer/projects/6 (private, linked to the repo). It
has a `Priority` single-select field that mirrors the priority labels, so the board can group and sort
by it.

### D-029 — Open questions as decision issues
**Decision:** Every ambiguity in the brief, and every scope question, is a `question` + `needs-decision`
issue in **M0**, with options and a **recommendation**. When the user answers, the decision is recorded
here as a `D-xxx` entry and becomes a line in the README **Assumptions** section. Questions specific to
one story are asked inside that story instead.
**Why:** The assignment says the Assumptions section is "read closely". Capturing the questions up front
makes it complete and traceable.

### D-030 — Subagent workflow (Superseded by D-046)
**Decision:** Every non-trivial story goes through four subagent roles, defined in `.claude/agents/`
and governed by `docs/SUBAGENTS.md`:

| Role | Agent | Model |
|---|---|---|
| Plan developer | `pizza-planner` | opus |
| Plan reviewer | `pizza-plan-reviewer` | opus |
| Developer | `pizza-developer` | sonnet; opus for the money/concurrency stories #10, #11, #16, #20 |
| Code reviewer | `pizza-reviewer` | opus |

The main session is the **orchestrator**. It briefs each agent with a fixed 6-part template (role,
story, plan file, branch, goal, context/constraints/return). It re-runs the checks itself, and it is the
only one that touches git remotes, GitHub, or the decision and work logs.
- **Two user gates:** the user approves the plan before development starts, and merges the PR.
- **Loop limits:** at most 2 REVISE / CHANGES REQUESTED rounds per gate, then escalate to the user.
- **Small changes** (typos, docs, one-line config) may skip planning; the WORKLOG must say so.

**Why:** The user asked for planner, plan-reviewer, developer and reviewer subagents, each with context
and a clear goal. Separating planning from building, and reviewing each independently, catches design
errors before code and ties every change back to the story's acceptance criteria. Opus is used where
judgment matters, and sonnet for executing an approved plan.
**Alternatives:** The installed `agent-kit` plugin (supervisor/developer/reviewer) has no
plan-review stage and no project-specific rules, so it's not used. One general agent per story would
have no independent review, which is weak evidence for the "AI leverage" grading criterion.

### D-031 — Plan file per story
**Decision:** Each story gets `docs/plans/<N>-<slug>.md`, created from `docs/plans/_TEMPLATE.md`. It
holds the Plan, Plan review, Implementation notes, Code review and Outcome. Each role writes only its
own section, and later rounds are appended, never overwritten.
**Why:** One durable, reviewable record per story, committed with the code. It lets the user review
plans in the editor and gives concrete evidence for the README's AI usage note (what the AI proposed,
what review caught, what was fixed).

### D-032 — Branch and PR per story (Superseded by D-033)
**Decision:** The work for story #N happens on `story/<N>-<slug>`. The developer commits locally in
small, logical steps (`Refs #N`). The orchestrator pushes and opens a PR ending with `Closes #N`, and the
user merges it. Then the board item moves to Done.
**Why:** It gives a readable commit history (graded under *Judgment*), a natural review point, and
issues that auto-close. Parallel stories run in separate git worktrees.

### D-033 — Feature branches and PRs for everything; `main` is never pushed to directly
**Decision (the user's):** No direct pushes to `main`, for code, docs and process changes alike. The
flow is: feature branch → PR → the user reviews → the user merges on GitHub → local `main` is
fast-forwarded. This extends D-032 to all changes.
- **Branch names:** `story/<N>-<slug>`, `fix/<slug>`, `docs/<slug>`, `chore/<slug>`.
- **Merge method:** create a merge commit, so the step commits stay visible. Delete the branch after
  merging.
- **Enforcement:** GitHub branch protection and rulesets aren't available for private repos on the free
  plan (the API returned 403, "Upgrade to GitHub Pro or make this repository public"). Instead, a
  committed `.githooks/pre-push` hook rejects any push whose target is `refs/heads/main`, enabled with
  `git config core.hooksPath .githooks`. `.gitattributes` keeps the hook's LF line endings on Windows.
  Bypassing it with `--no-verify` is forbidden for humans and agents alike.
**History note:** the first four commits (skeleton, tracking docs, board link, subagent docs) were pushed
straight to `main` before this rule existed. Everything after goes through PRs, starting with this
change (`chore/feature-branch-workflow`).
**Alternatives:** GitHub Pro, or a public repo, would allow real server-side protection (required PR,
no force push). That's worth it if the repo is made public for the submission.
**Applied (2026-11-17, after PR #39 merged, approved by the user):**
- Repo settings: `allow_merge_commit=true`, `allow_squash_merge=false`, `allow_rebase_merge=false`,
  `delete_branch_on_merge=true`. Merge commits are the only option, and head branches are deleted
  automatically.
- `core.hooksPath=.githooks` is set in the main working clone.

### D-034 — Playwright for UI tests; Vitest stays for unit tests
**Decision (the user's):** UI behaviour is tested with **Playwright** (`@playwright/test` **1.65.1**,
pinned exactly, with Chromium) driving the real Quasar app in a browser. Vitest and Quasar's testing
extension (D-023) stay for unit tests of stores and the API client. Story #41 sets this up.
- Specs live in `client/test/e2e/`, and Vitest only picks up `client/test/vitest/__tests__/`, so the two
  runners never see each other's files. `npm test` is still Vitest only. `npm run test:e2e` /
  `test:e2e:ui` run Playwright.
- `client/playwright.config.ts` starts its own `quasar dev` on a **dedicated port, 9100** (override
  with `PLAYWRIGHT_PORT`) and reuses it if it's already running (outside CI). The port isn't the
  normal dev port 9000 because Playwright would otherwise silently reuse a dev server you already
  have open, possibly from another checkout or worktree, and test the wrong code. `/api` is still
  proxied by the dev server (D-011).
- #36 (frontend component tests, which were planned with Vitest) will use Playwright for UI behaviour.

**Why E2E rather than Playwright component testing:** `@playwright/experimental-ct-vue` is still
experimental, and it builds components with its own Vite config instead of the Quasar CLI's, so
Quasar's auto-import, boot files and `quasar.config.ts` setup would have to be copied by hand. E2E
against `quasar dev` tests exactly what the customer sees, with no second build setup.
**Why keep Vitest:** store and API-client logic (debounce, ignoring stale quote responses, idempotency
keys) is quicker and more precise to test without a browser.
**Pinned exactly** (`-E`): the npm package and its downloaded browser builds are versioned together,
so a caret range could drift away from the installed browsers.
**Alternatives:** Playwright only (drop Vitest): one tool, but store logic would only be testable
through the browser. Cypress: the user chose Playwright.

### D-035 — Order of the first UI work
**Decision (the user's):** To get a usable order screen quickly: first the Playwright harness (#41, a
small chore PR), then **#25** (kiosk start screen and draft-order store, which needs no backend), then
**#8** (data-driven menu and `GET /api/menu`) → **#26** (pizza customizer).
**Why:** #25 is the only UI story with no backend or M0 dependency. #26 needs #8's menu API, and #27
(running total) needs #14, which waits on the M0 pricing questions #2 and #3.
**Alternatives:** #8 + #26 first (no UI to see until both are done), or #26 against a mocked menu
(breaks #26's "nothing hard-coded" acceptance criterion until #8 lands).

### D-036 — A secondary session works in its own git worktree
**Decision:** A second Claude Code session that works alongside the main one uses its own git worktree
(`git worktree add .claude/worktrees/<N>-<slug> -b <branch> origin/main`), not the shared checkout.
`.claude/worktrees/` is excluded locally through `.git/info/exclude`, so nothing has to be committed.
The branch's upstream is unset after `git worktree add`, so a bare `git push` can't target `main` (the
pre-push hook, D-033, would reject that anyway).
**Why:** Both sessions share one working directory. One session switching branches or pulling would
change files under the other. A worktree gives each session its own branch and files and shares the
same `.git`, so the hook and config still apply.
**Trade-off:** Each worktree needs its own `npm ci`, and the two sessions can still pick the same
`D-xxx` number or both append to `WORKLOG.md`. Those conflicts are resolved when merging `main` into the
branch. The PR says which session wrote it.

---

## M0 decisions (answers to questions #1–#7)

Each of these becomes a line in the README **Assumptions** section (#37).

### D-037 — Scope: full kiosk vision (#1) (Superseded by D-046)
**Decision (the user's, against the recommendation):** The full self-order kiosk experience is in scope:
a start screen (#25), a simulated payment step (#29) and a kitchen view (#33), on top of everything
the assignment requires. Those three stories move from `P2-stretch` to `P1-should`.
**Build order is unchanged:** the assignment's required path (pricing, lifecycle, invariant tests,
README) still comes first, so a working, graded submission exists at every point.
**Trade-off:** this is well beyond the brief's 3-hour timebox. The README will report the real time
spent and explain the larger scope as a deliberate choice. Recommended was P0 → P1 → P2-if-time.

### D-038 — Pricing math and rounding (#2)
**Decision:** Accepted after research into real POS and tax practice (sources in WORKLOG session 4):

| Rule | Decision | Basis |
|---|---|---|
| Rounding mode | Round half **up** (away from zero) to cents. Always pass `MidpointRounding.AwayFromZero`, because .NET's default `Math.Round` is banker's rounding (2.5 → 2). | The US sales-tax half-cent rule |
| Topping price | `round(size.basePrice × category.rate)` per topping, per pizza | Menu prices are shown in cents |
| Line total | unit price × quantity (already exact) | — |
| Promo discount | Computed on the subtotal, rounded once, capped at the subtotal | The brief: "discount on the total order before tax" |
| Tax | 6.5% of (subtotal − discount), **rounded once on the order total**, not per line | Store-funded discounts reduce the taxable amount; several states require invoice-level tax |
| Tip | **Not taxed**, added after tax | Voluntary tips aren't subject to sales tax |
| **Tip % base** | **Pre-tax, after discount:** `tip = round((subtotal − discount) × tip%)`. Configurable with `Pricing:TipBase` = `PreTax` (default) or `PostTax` (post-discount + tax, the literal "total bill"). | The user's choice. It's Toast's default and common etiquette. |
| SAVE5 threshold | Strictly **> $20.00**, on the subtotal **before** discount, tax and tip | The brief: "order total before tax is over $20" |

**Formula:**
```
toppingPrice = round(sizeBase × categoryRate)
lineUnit     = sizeBase + Σ toppingPrice
lineTotal    = lineUnit × quantity
subtotal     = Σ lineTotal
discount     = min(subtotal, round(promo(subtotal)))
taxable      = subtotal − discount
tax          = round(taxable × taxRate)
tipBase      = taxable                  (PreTax, default)  |  taxable + tax  (PostTax)
tip          = amount  |  round(tipBase × tip%)
total        = taxable + tax + tip
```
**Ambiguity to record:** "percentage of the total bill" could be read as after tax. The brief adds "before
tax" wherever it means a pre-tax amount and uses "bill" only here. We follow industry practice (pre-tax)
and keep the base switchable.

### D-039 — Pricing runs on the server only (#3)
**Decision:** The order service is the single source of truth. The UI calls `POST /api/orders/quote` on
every change (debounced) and only **displays** server-computed money; it never does money arithmetic.
On submit, the client sends the total it displayed. The server re-prices, and if the result differs it
returns **409 Price changed** with the new breakdown for the customer to confirm.
**Why:** One implementation can't drift. It directly satisfies "the total the customer sees … must match
the total the restaurant charges".

### D-040 — Cancel deletes the order; idempotency outlives orders (#4)
**Decision (the user's, against the recommendation):** Cancelling an order (allowed only while Not
Started) **deletes** it: `DELETE /api/orders/{id}` returns **204**. Afterwards, `GET` returns **404** and
a repeated `DELETE` returns **404**. The UI treats a 404 on a cancel retry as "already cancelled".
**Consequences handled in the design:**
- **Idempotency records live in their own table** (key, request hash, order id, outcome), not on the
  order row. A slow, retried *submit* that arrives after a delete must **not** re-create the order. It
  returns **409** "This order was cancelled" instead of creating a new one.
- The delete is **conditional** on `status = NotStarted` in one statement, so a delete racing the
  kitchen's "start" has exactly one winner (see #20).
- No `Cancelled` status. The statuses are exactly the brief's four.
**Status flow (assumption):** strictly linear, NotStarted → Started → ReadyForPickup → Delivered. No
skipping, no going back.
**Recommended was** a terminal `Cancelled` status, which keeps history and makes a repeated cancel
idempotent. The README notes the trade-off.

### D-041 — Simulated payment in scope (#5)
**Decision (the user's):** The kiosk flow includes a **clearly labelled simulated** payment step. No real
card data, no payment provider (the brief forbids real payments). A decline path demonstrates the error
handling. #29 moves to `P1-should`.
**Still to settle when #29 is planned** (proposed defaults): pay before the order is sent to the kitchen;
an update while Not Started shows a simulated charge or refund for the difference; a cancel (delete)
shows "payment voided (simulated)".

### D-042 — Kitchen simulation (#6)
**Decision:** `POST /api/kitchen/orders/{id}/advance` moves an order to its next status. It's
deterministic and is what tests and demos use. Optionally, a background auto-advance timer runs behind
`Kitchen:AutoAdvance:Enabled` (off by default; on in docker-compose for demos) with a configurable
interval. Both go through the same transition rules (#20). A kitchen view (#33) calls the same endpoint.

### D-043 — Validation limits and SAVE5 eligibility (#7)
**Decision:** The recommended limits:

| Input | Rule |
|---|---|
| Line items | 1–25 per order (an empty order is rejected) |
| Quantity | Integer 1–20 per line |
| Toppings | A set per pizza: no duplicates; any number from the menu |
| Tip (amount) | $0.00–$500.00, at most 2 decimals |
| Tip (percent) | 0–100%, at most 2 decimals |
| Unknown size or topping codes | 400 with field errors |
| Promo codes | Trimmed and case-insensitive. Unknown → a clear error |
| **SAVE5 on ≤ $20.00** | **Rejected** with a message ("SAVE5 needs an order over $20.00 before tax"); the code isn't applied |
| SAVE5 + ILOVEPIZZA | Rejected (one code per order; conflict rule in the promo engine) |

### D-044 — Kiosk flow and route guard (#25)
`/` is the start screen and `/order` is the builder (`meta.building`). A guard sends building routes
without a draft to `/`, and `/` with a draft to `/order`, so a **confirmed** Cancel order is the only
exit. `useEndOrder().endOrder()` is the single "clear the draft → start screen" path, and the #44 idle
timeout (cut, D-046) would reuse it. *Alternatives:* a "Continue / Start new" choice; a separate layout.

### D-045 — Draft-order store and persistence (#25)
A Pinia setup store holds `{ lines[{ id, sizeCode, toppingCodes, quantity }], promoCode, tip{ mode,
value: string } }`: menu codes only, no prices (D-039). It's persisted to **sessionStorage** in a
versioned envelope, shape-checked on restore, and fail-safe (memory-only, one warning). No new
dependency. *Alternatives:* `pinia-plugin-persistedstate`, `localStorage` (outlives the kiosk session).

### D-046 — Timebox reset: batched PRs, slim process, cut list (supersedes D-030, D-037)
**Decision (the user's, after a process review):** Planning one P1 screen took four opus runs and an
871-line plan, so the rest is delivered in **4 batched PRs**, with one commit per story and issues closed
from the PR body:
- **A:** pricing #8–#14, #34
- **B:** order service #15–#22, #35
- **C:** UI #26–#28, #30–#32
- **D:** README #37

**Process:** the main session implements A and C directly. B gets one short plan (≤ ~150 lines) for
idempotent submit and the cancel-vs-kitchen race. One reviewer pass per PR; the orchestrator re-runs the
checks; one user approval per PR; sonnet for developer and UI work.
**Tracking:** 2–3 line decisions, one WORKLOG entry per PR, and docs ride inside the feature PR.
**Cut to the README cut list:** #44 idle timeout, #29 simulated payment, #33 kitchen view (the D-042
endpoint plus curl is enough), #23 structured logging, #24 OpenAPI UI (unless free), #36 frontend
component tests, #38 CI.
**Budget order if time runs short:** cut C before B. The brief weights pricing and lifecycle over UI.

### D-047 — How the order lifecycle guarantees are implemented (PR B)
- **One order per Idempotency-Key:** the key is the primary key of `idempotency_records`, written in the
  same transaction as the order. The loser of a concurrent duplicate replays the winner. A key whose
  order was deleted returns 409 `order.cancelled`, so the order is never re-created.
- **Cancel vs kitchen start:** each is one conditional statement (`DELETE/UPDATE … WHERE status =
  'NotStarted'`), and Postgres row locks make the waiting statement re-check its `WHERE`. Exactly one
  wins, as proven by the deterministic lock-holding tests.
- **Updates** use `xmin` optimistic concurrency (ETag / If-Match).
- **Advance doesn't retry** after losing a race: 409 `order.status_changed` (deviation from the plan,
  which said "retry once"), so a stale request can never double-step.
- **An update that loses a race** returns 404 (cancelled) or 409 (started), never 500. A cancel shows up as
  a foreign-key violation on the new lines, and an advance as an `xmin` conflict.
- **A PUT retried after it already succeeded** gets 412, because its ETag is now stale. That's safe (no
  double effect): the client re-fetches and sees its change applied.
- *Alternatives:* `SELECT … FOR UPDATE` row locks, or serializable transactions. Both are heavier, with
  no extra guarantee here.
