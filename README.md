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
    Models/              Customer, Account, Merchant, Transaction, Rule, Alert
    Fraud/               IFraudRule, the six rules, RuleEngine, TransactionScorer
    Simulation/          Fake transaction stream and fraud patterns
    Data/                DbContext, seeder, EF Core migrations
    Controllers/
  FraudMonitor.Tests/    xUnit tests
web/                     Angular app (served by nginx in Docker)
docker-compose.yml       db + api + web
```

## How fraud scoring works

A background service (`TransactionSimulator`) creates a fake card transaction every 2–5 seconds. Each one goes through the rules engine before it is saved:

1. `TransactionScorer` loads the account's last 30 days of transactions and the current rule settings from the database.
2. `RuleEngine` runs every enabled rule. Each rule is its own `IFraudRule` class with its own unit tests.
3. The weights of the rules that fire are added up (capped at 100). A score of **60 or more** flags the transaction and opens an **Alert**, with one **AlertReason** per fired rule explaining why (e.g. *"4 transactions in 2 minutes"*).

| Rule | Fires when | Weight | Tunable threshold (default) |
|------|------------|-------:|-----------------------------|
| Large amount | Amount is more than N× the account's 30-day average | 30 | N = 5 |
| Velocity | N or more transactions on one account within 5 minutes | 35 | N = 3 |
| Impossible travel | Two transactions 100+ miles apart within 24 h needing travel faster than N mph | 45 | N = 500 |
| Odd hour | 1–5 a.m. local time, for an account where less than N of its history happened then | 15 | N = 0.10 |
| Risky category | Gift card, crypto exchange or wire service purchase above $N | 20 | N = 500 |
| Card testing | N or more charges under $2 in the past hour, then one of $100 or more | 40 | N = 3 |

Weights, thresholds and on/off switches live in the `Rules` table, so they can be tuned without a redeploy.

No single rule reaches 60 on its own: an alert needs evidence from more than one rule, which keeps false positives down. To show the rules working, the simulator injects known fraud patterns about 6% of the time:

| Pattern | What happens | Rules that catch it |
|---------|--------------|---------------------|
| Card testing | Four sub-$2 online charges, then a $900–1,800 electronics purchase | Card testing + velocity + large amount |
| Impossible travel | A purchase at home, then a big in-store purchase 1,000+ miles away 30 s later | Impossible travel + large amount |
| Spree | Four escalating $1,200+ electronics/retail purchases in 90 s | Velocity + large amount |
| Cash-out | Three escalating gift card / crypto / wire purchases in 80 s | Velocity + risky category + large amount |

Normal traffic stays in the customer's home city (or online), and each account rests at least 3 minutes between normal purchases, so ordinary spending doesn't trip the location or velocity rules by accident.

Simulator settings (on/off, speed, how often patterns are injected) are in the `Simulator` section of `appsettings.json`.

## Run everything with Docker

```bash
docker compose up --build
```

| What              | URL                              |
|-------------------|----------------------------------|
| Web app           | http://localhost:4200            |
| API Swagger page  | http://localhost:5000/swagger    |
| PostgreSQL        | localhost:5432 (fraud / fraud_dev_password) |

On startup the API applies migrations and seeds 50 customers (with 1–3 accounts each), 200 merchants and 30 days of normal transaction history, then starts the simulator. The home page shows live counts of transactions and alerts.

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
- [x] Week 2: transaction simulator with injected fraud patterns, six-rule engine, alerts and reasons, tests
- [ ] Week 3: controllers, JWT auth with roles, audit log, CI
- [ ] Week 4: login, live feed over SignalR, alert queue
- [ ] Week 5: customer view, metrics header, polish
- [ ] Week 6: deploy, README screenshots, demo video
