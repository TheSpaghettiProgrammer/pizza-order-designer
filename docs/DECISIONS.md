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
| D-030 | 2026-11-17 | Four-role subagent workflow (planner → plan reviewer → developer → reviewer) with two user gates | Accepted |
| D-031 | 2026-11-17 | One plan file per story in `docs/plans/`, the single artifact every role writes to | Accepted |
| D-032 | 2026-11-17 | Branch per story (`story/<N>-<slug>`), PR with `Closes #N`, user merges | Superseded by D-033 |
| D-033 | 2026-11-17 | No direct pushes to `main`: every change goes through a feature branch + PR the user merges, enforced by a pre-push hook | Accepted |

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

### D-030 — Subagent workflow
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
