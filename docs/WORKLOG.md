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

### Open items
- ~~Upgrade host Node~~ Done: Node 24.22.1 / npm 12.2.3 (D-021).
- ~~Decide on the auto-filled `author`~~ Done: the user chose to remove the email; `author` is now the
  name only.
- No EF migration exists yet. The first one is created when the first entities are modeled.
- Replace the placeholder test once real endpoints exist (D-017).
- `git init` + first commit when the user is ready.
