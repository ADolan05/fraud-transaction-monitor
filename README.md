# Fraud Transaction Monitor

A real-time fraud monitoring tool where a rules engine flags suspicious transactions and analysts triage them through an Angular dashboard.

> **All data is synthetic.** Customers, merchants and accounts are generated with [Bogus](https://github.com/bchavez/Bogus). There are no real card numbers, names or accounts, and account numbers are only ever stored masked (`****4821`).

## Stack

| Layer     | Choice                                        |
|-----------|-----------------------------------------------|
| Front end | Angular 22 + TypeScript                       |
| Back end  | ASP.NET Core 8 Web API (C#)                   |
| Database  | PostgreSQL 17 via Entity Framework Core 8     |
| Real-time | SignalR *(planned)*                           |
| Auth      | JWT with Analyst and Admin roles *(planned)*  |
| DevOps    | Docker Compose                                |
| Testing   | xUnit (API), Vitest (web)                     |

## Repository layout

```
api/
  FraudMonitor.Api/      ASP.NET Core Web API
    Models/              Customer, Account, Merchant, Transaction
    Data/                DbContext, seeder, EF Core migrations
    Controllers/
  FraudMonitor.Tests/    xUnit tests
web/                     Angular app (served by nginx in Docker)
docker-compose.yml       db + api + web
```

## Run everything with Docker

```bash
docker compose up --build
```

| What              | URL                              |
|-------------------|----------------------------------|
| Web app           | http://localhost:4200            |
| API Swagger page  | http://localhost:5000/swagger    |
| PostgreSQL        | localhost:5432 (fraud / fraud_dev_password) |

On startup the API applies migrations and seeds 50 customers (with 1–3 accounts each) and 200 merchants. The home page shows those counts, which confirms that the web app, API and database are all connected.

To reset the database: `docker compose down -v`.

## Run locally without Docker (for development)

Requires the .NET 8 SDK and Node.js 22+.

```bash
# 1. Database only
docker compose up db

# 2. API on http://localhost:5000
cd api
dotnet run --project FraudMonitor.Api

# 3. Angular on http://localhost:4200 (proxies /api to port 5000)
cd web
npm install
npm start
```

## Tests

```bash
cd api && dotnet test          # xUnit
cd web && npx ng test --watch=false   # Vitest
```

## Database migrations

The EF Core CLI is pinned in `api/.config/dotnet-tools.json`.

```bash
cd api
dotnet tool restore
dotnet ef migrations add <Name> -p FraudMonitor.Api -o Data/Migrations
```

## Roadmap

- [x] Week 1: repo, API and Angular scaffolding, Docker Compose, EF Core models and first migration, seed data
- [ ] Week 2: transaction simulator, fraud rules engine, rule unit tests
- [ ] Week 3: controllers, JWT auth with roles, audit log, CI
- [ ] Week 4: login, live feed over SignalR, alert queue
- [ ] Week 5: customer view, metrics header, polish
- [ ] Week 6: deploy, README screenshots, demo video
