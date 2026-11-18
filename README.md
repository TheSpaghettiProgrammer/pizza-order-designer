# Pizza Order Designer

A take-home project: design a pizza and place an order.

| Tier | Tech | Location |
|------|------|----------|
| Frontend | Quasar 2 (CLI, Vite) + Vue 3 + TypeScript (Router, Pinia) | [`client/`](client/) |
| API | ASP.NET Core Web API on .NET 10 (controllers) | [`server/`](server/) |
| Data | EF Core 10 + Npgsql → PostgreSQL 17, code-first migrations | [`server/src/PizzaOrderDesigner.Data`](server/src/PizzaOrderDesigner.Data) |

Design decisions are recorded in [docs/DECISIONS.md](docs/DECISIONS.md), and the session-by-session log
is in [docs/WORKLOG.md](docs/WORKLOG.md).

## Run everything (Docker)

Requires Docker only.

```bash
docker compose up --build
```

- App: http://localhost:8080
- API (direct): http://localhost:5080/api/health
- Postgres: `localhost:5432` (db `pizza_order_designer`, user `pizza`, password `pizza_dev`; dev only)

The API applies EF Core migrations on startup.

## Local development

Requires the .NET 10 SDK, Node 24 (or ^22.22 / 26), and Docker for the database.

```bash
docker compose up -d db
```

API (http://localhost:5158), from `server/`:

```bash
dotnet tool restore
dotnet watch --project src/PizzaOrderDesigner.Api
```

Frontend (http://localhost:9000, proxies `/api` to the API), from `client/`:

```bash
npm install
npm run dev
```

### Tests

```bash
dotnet test server/PizzaOrderDesigner.slnx
```

```bash
npm --prefix client test
```

### Contributing workflow

`main` only changes through pull requests: create a feature branch, open a PR, review it, merge it. Enable
the repo's hook once per clone so an accidental push to `main` is rejected:

```bash
git config core.hooksPath .githooks
```

### Adding a migration

From `server/`:

```bash
dotnet ef migrations add <Name> -p src/PizzaOrderDesigner.Data -s src/PizzaOrderDesigner.Api
```
