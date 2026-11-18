# Pizza Order Designer — working agreements

This is a job-interview take-home. The user wants **every action and decision tracked**.

## Tracking (mandatory, for the main session and every subagent)
- **docs/DECISIONS.md**: add a `D-xxx` row to the index table and a section (Decision / Why /
  Alternatives or Trade-off) for every meaningful technical or process decision, including ones the
  user makes. Never silently change a decision: mark the old one `Superseded by D-xxx`.
- **docs/WORKLOG.md**: append to the current session's entry what was done, problems hit and how they
  were fixed, verification results, and open items. Start a new dated `## Session N` heading for each
  new working session.
- Keep README.md's run instructions accurate whenever setup changes.

## Stack and layout
- `server/`: .NET 10 (`global.json` pins SDK 10.0.404), `PizzaOrderDesigner.slnx`
  - `src/PizzaOrderDesigner.Api`: controllers, DI, health checks, migrate-on-startup flag
  - `src/PizzaOrderDesigner.Data`: `PizzaDbContext`, `IEntityTypeConfiguration` classes, migrations
  - `tests/PizzaOrderDesigner.Api.Tests`: xUnit
- `client/`: Quasar 2 CLI (`@quasar/app-vite`, `quasar.config.ts`) + Vue 3.5 + TypeScript, Router
  (history mode), Pinia, ESLint + Prettier. Vitest via `@quasar/testing-unit-vitest`, with tests in
  `client/test/vitest/__tests__/`. Dev server on :9000. Build output is `dist/spa`.
- `docker-compose.yml`: `db` (postgres:17), `api` (:5080), `web` (nginx, :8080)

## Conventions
- EF Core packages stay on **10.0.x** (EF 11 targets net11 only). Pin versions explicitly.
- Entity mappings go in `IEntityTypeConfiguration<T>` classes in the Data project, not in `OnModelCreating`.
- The frontend calls relative `/api/...` only (proxied by `quasar.config.ts` devServer in dev, nginx in
  containers). No CORS.
- Use Quasar components (`q-*`) and the Quasar CLI (`npx quasar ...`). Quasar's API docs are at
  https://quasar.dev.
- Migrations, from `server/`: `dotnet ef migrations add <Name> -p src/PizzaOrderDesigner.Data -s src/PizzaOrderDesigner.Api`

## Environment quirks (this machine)
- Host Node 24.22.1 / npm 12.2.3 (global npm lives in `%APPDATA%\npm`). npm 12 blocks dependency
  install scripts by default. The `@parcel/watcher` warning is harmless.
- Docker Desktop 4.99. An older 4.58 crashed on stale AF_UNIX sockets in `%LOCALAPPDATA%\Docker\run` and
  `%LOCALAPPDATA%\docker-secrets-engine`. If that recurs, rename the folder aside while Docker is stopped.

## Repo and work tracking
- GitHub (private): https://github.com/TheSpaghettiProgrammer/pizza-order-designer, default branch `main`.
- All work is tracked as GitHub issues on the project board
  (https://github.com/users/TheSpaghettiProgrammer/projects/6). The board has a Priority field. Move
  items through Status (Todo → In Progress → Done) as work progresses. Labels: `story` / `question` /
  `needs-decision`; `P0-required` / `P1-should` / `P2-stretch`; `area:*`. Milestones: M0 Decisions →
  M4 Quality & deliverables.
- Small, meaningful commits that reference issues (`Refs #N`, `Closes #N`). Commit history is graded.
- Answers to M0 questions (#1–#7) become `D-xxx` entries and README **Assumptions**. Don't build
  anything that depends on an unanswered question without flagging it.
- The brief is in `docs/ASSIGNMENT.md`. 3-hour timebox: prioritize P0, keep a cut list.

## Subagents
- Follow **`docs/SUBAGENTS.md`** for every story: `pizza-planner` → `pizza-plan-reviewer` → **user
  approves the plan** → `pizza-developer` → `pizza-reviewer` → orchestrator opens the PR → **user
  merges**.
- Agent definitions are in `.claude/agents/`. Each story has a plan file `docs/plans/<N>-<slug>.md`
  (from `_TEMPLATE.md`) where every role writes only its own section.
- Brief every subagent with the 6-part template in `docs/SUBAGENTS.md` §5. Subagents start cold.
- Only the orchestrator (main session) pushes, opens PRs, touches GitHub issues, or edits
  `DECISIONS.md`/`WORKLOG.md`. Re-run the checks yourself before reporting a subagent's result.
