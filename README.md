<div align="center">

# ✦ LuminaVault

**Your personal vault for everything you own and everything you spend** — a self-hosted home inventory with an interactive 3D planner, paired with a full personal-finance suite.

[![Stars](https://img.shields.io/github/stars/dominictosku/LuminaVault?style=for-the-badge&color=7c3aed&labelColor=0f0f1a)](https://github.com/dominictosku/LuminaVault/stargazers)
[![Forks](https://img.shields.io/github/forks/dominictosku/LuminaVault?style=for-the-badge&color=6d28d9&labelColor=0f0f1a)](https://github.com/dominictosku/LuminaVault/network/members)
[![Issues](https://img.shields.io/github/issues/dominictosku/LuminaVault?style=for-the-badge&color=a78bfa&labelColor=0f0f1a)](https://github.com/dominictosku/LuminaVault/issues)
[![License](https://img.shields.io/github/license/dominictosku/LuminaVault?style=for-the-badge&color=8b5cf6&labelColor=0f0f1a)](LICENSE)

[![Angular](https://img.shields.io/badge/Angular-21-DD0031?style=flat-square&logo=angular)](https://angular.dev)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![Three.js](https://img.shields.io/badge/Three.js-r171-000000?style=flat-square&logo=threedotjs)](https://threejs.org)
[![TailwindCSS](https://img.shields.io/badge/Tailwind-v4-06B6D4?style=flat-square&logo=tailwindcss)](https://tailwindcss.com)
[![PrimeNG](https://img.shields.io/badge/PrimeNG-21-007ad9?style=flat-square)](https://primeng.org)

</div>

---

> Track every item in your home — from the living-room cabinet to the left shelf of the hallway wardrobe — *and* every franc that moves through your accounts. One app, one database, one glassmorphism UI.

<br>

## Screenshots

<div align="center">

### Dashboard
![Dashboard screenshot](docs/screenshots/dashboard.png)

### 3D Planner
![3D planner screenshot](docs/screenshots/planner.png)

### Item detail
![Item form screenshot](docs/screenshots/item-form.png)

</div>

> **Note:** Drop your own screenshots into `docs/screenshots/` to replace these placeholders.

<br>

## Features

### 🏠 Home inventory
- **House → Room → Furniture → Container → Item** hierarchy — place anything anywhere
- **3D planner** — animated Three.js walkthrough with GSAP; click rooms to fly inside, click furniture to inspect it
- **Per-kind 3D models** — procedurally generated geometry for cabinets, shelves, wardrobes, beds, sofas and more; falls back to custom glTF if a `/models/{Kind}.glb` exists
- **Per-item glTF upload** — attach a `.glb` or `.gltf` model to any item and see it floating in the 3D view
- **Photo gallery** — upload multiple photos per item
- **Document attachments** — keep receipts, manuals and warranty PDFs next to the item they belong to
- **Full-text search & filters** — by name, brand, model, serial number, tags or category

### 💰 Personal finance
- **Multi-account ledger** — checking, savings, cash, credit cards, investments, crypto, loans; per-account currency (default CHF) and starting balance
- **Dated exchange rates** — convert multi-currency accounts, subscriptions and historical transaction analytics into CHF aggregate totals
- **Transactions** — income / expense / transfer with categories, tags, payee, status (pending / cleared / reconciled), plus trade kinds (buy / sell / dividend / fee) with symbol, quantity and price-per-unit for investment & crypto accounts
- **Transaction splits** — one Income/Expense can be split across multiple categories (e.g. one grocery receipt = 70 Food + 30 Household). Cash impact still runs off the single Amount; budget/statistics aggregation routes each split to its own category bucket
- **Category rules** — auto-categorize uncategorized/manual imports by matching payee, description or notes
- **Holdings, analytics & live prices** — symbol-level positions per investment/crypto account with average cost, last price, realized/dividend/fee return, allocation, top movers and unrealized P&L; one-click refresh fetches quotes from pluggable providers (**Finnhub** for stocks, **CoinGecko** for crypto)
- **Budgets** — per-category monthly limits with spend tracking
- **Savings goals** — target amount, current amount, status and due-date tracking for funds or payoff plans
- **Loan amortisation** — track mortgages, car and personal loans with principal, rate, term and optional extra monthly payments; per-loan schedule shows monthly principal/interest split, year-level totals, and how much earlier extra payments pay the loan off
- **Swiss tax export** — year-end bundle for the Wertschriftenverzeichnis & wealth declaration: every crypto/stock held on Dec 31 (rolled forward from Buy/Sell history), Dec 31 cash account balances, and income/expense aggregates for the year. Available as on-screen tables or as a multi-sheet ODS download; price rows are flagged when the last known quote is older than Dec 31 so you can spot which lines to override before filing
- **Recurring subscriptions** — calendar-aware billing interval (every N days / weeks / months / years with one-click presets and a free-form count for anything else), next-due date, auto-renew, due forecast generation, optional document attachments (contracts, invoices); monthly-cost math divides by the *unit* (so a CHF 120/year subscription reports CHF 10/month, not CHF 9.99) and due-dates advance via calendar months / years (Jan 31 → Feb 28, Feb 29 → Feb 28 next year)
- **Monthly summaries & reconciliation** — per-account opening/closing balances, income/expense totals, reconciliation notes
- **Balance snapshots** — record actual vs. expected balance over time to catch drift
- **Net worth history** — daily snapshots of (cash + holdings market value + inventory), rendered as a line chart on the dashboard with delta vs. earliest point; manual "snapshot now" button seeds the curve before the first cron run
- **Cash-flow forecast** — replays scheduled subscription dues and pending transactions forward 14/30/60/90/180 days, plotting projected liquid cash as a line chart with the lowest-balance date called out and a warning banner if the projection dips below zero
- **Notifications & alerts** — in-app inbox with unread-count badge in the sidebar; a background scanner fires alerts for subscriptions due soon, budget overruns, and forecast dips below zero. Dedup by stable `Source` key means re-scans upsert instead of spam; dismissed alerts resurface if the underlying condition still holds
- **Statistics** — charts and breakdowns for spend, income and category trends, plus a **money-flow Sankey** (income categories → Net → expense categories) over the last 12 months with a Surplus/Shortfall balance node and hover highlighting
- **ODS import/export** — round-trip your data with LibreOffice Calc spreadsheets (with a preview step before import); splits ride along inline in the Transactions sheet as `Category=Amount[|Notes];…`
- **Bank CSV import** — preview statement files, map columns, import into a chosen account, and skip likely duplicate transactions

### 🔐 Platform
- **Single-user JWT auth** — stateless, BCrypt-hashed password, token stored in localStorage. Registration is open only until the first account is created; every later `/api/auth/register` is rejected with `409 Conflict`, so the instance can never grow a second user
- **Two-factor authentication (TOTP)** — optional authenticator-app second factor (Google Authenticator, Authy, 1Password…) with QR enrolment, 10 one-time recovery codes, and a password-confirmed disable; login becomes a two-step password → code challenge once enabled
- **Security controls** — tight per-IP login throttling (10/min) plus register throttling, in-app password change, hardening response headers (CSP, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`) on both the API and the SPA shell, and proxy-aware client-IP resolution so rate limiting throttles the real caller rather than the reverse proxy
- **Glassmorphism UI** — dark, frosted-glass design with smooth transitions, built on PrimeNG + Tailwind v4
- **Self-hosted, single-file SQLite** — your data never leaves your machine
- **Zoneless Angular + Signals** — fast, modern change detection

<br>

## Tech stack

| Layer | Technology |
|---|---|
| Frontend | Angular 21 · Standalone components · Signals · Zoneless CD |
| UI | PrimeNG 21 · PrimeIcons · Tailwind CSS v4 · Glassmorphism |
| 3D | Three.js r171 · OrbitControls · GLTFLoader · GSAP |
| Backend | ASP.NET Core 10 · Minimal APIs · EF Core 9 · SQLite |
| Auth | JWT Bearer · BCrypt.Net |
| Import/Export | ODS (OpenDocument Spreadsheet) |

<br>

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org)

### Backend

```bash
cd LuminaVault-Backend
dotnet run
# API available at http://localhost:5256
```

The SQLite database (`data/luminavault.db`), uploads (`data/uploads/`), logs (`data/logs/`), and optional scheduled backups (`data/backups/`) are created automatically on first run. The backend uses **EF Core Migrations** — `db.Database.Migrate()` runs on startup and applies any pending migrations idempotently. Default asset and finance categories are seeded the first time the app starts.

> **Upgrading from a pre-migrations build?** This branch switches from `EnsureCreated` + hand-rolled `ALTER TABLE` patches to real EF migrations. The first migration is `Initial`, which models the full current schema. Existing dev databases were created without an `__EFMigrationsHistory` row, so EF won't recognise them. **Move your old database aside** (root-level `luminavault.db` or `data/luminavault.db`) and let the app create a fresh schema on next start. Open an issue if you need a data-preserving upgrade path.

To add a schema change later: `cd LuminaVault-Backend && dotnet ef migrations add YourChangeName`. The new migration is applied automatically on the next `dotnet run`.

### Frontend

```bash
cd LuminaVault-Frontend
npm install
npm start
# App available at http://localhost:4200
```

### Self-host with Docker

For a one-command production deployment, the repo ships a [docker-compose.yml](docker-compose.yml) with two services: the .NET API and an nginx-fronted Angular SPA. The frontend container serves the static bundle and reverse-proxies `/api/*` to the backend, so the whole stack lives on a single port and origin (no CORS to configure).

```bash
cp .env.example .env
# generate a 32+ char JWT key (any random secret works):
openssl rand -base64 48 | tr -d '\n' > /tmp/k && echo LUMINA_JWT_KEY=$(cat /tmp/k) >> .env
# first-user registration secret for Production:
openssl rand -base64 32 | tr -d '\n' > /tmp/s && echo LUMINA_SETUP_SECRET=$(cat /tmp/s) >> .env

docker compose up -d --build
# App available at http://localhost:8080
```

A single named volume (`luminavault-data`) holds the SQLite DB, `uploads/`, `logs/`, and (if enabled) `backups/`. Swap it for a host bind mount in `docker-compose.yml` if your backup tooling already covers a host path. The backend reads `LUMINA_DATA_DIR=/data` and writes everything under that one mount.

The compose file does **not** include TLS. Front it with your favourite terminator (Caddy, Traefik, Cloudflare Tunnel, Nginx Proxy Manager) before exposing the port publicly. The bundled nginx already strips its own CORS concerns by serving SPA and API from the same origin.

### Tests

```bash
cd LuminaVault-Backend.Tests
dotnet test
```

The test project boots the real API via `WebApplicationFactory<Program>` against a per-test SQLite file under the system temp dir, so tests don't share state. Coverage spans the load-bearing finance math (balance recompute after writes, transfers debiting both sides, weighted-average holdings, budget spend tracking), the inventory CRUD plumbing, the ODS export/import round-trip, the health endpoint, settings, auth, and the `{ error: "..." }` validation envelope shape the frontend depends on.

### Git hooks

A tracked `pre-commit` hook in `.githooks/` runs the test suite(s) that match your staged changes — backend tests fire if any file under `LuminaVault-Backend*/` is staged, frontend tests fire if any file under `LuminaVault-Frontend/` is staged. Enable once after cloning:

```bash
git config core.hooksPath .githooks
```

### Backups

LuminaVault is self-hosted, so backup is your responsibility — but the app helps. Hit `GET /api/data/backup` (authenticated) to download a zip containing a consistent SQLite snapshot of `luminavault.db` plus the entire `uploads/` folder (item photos, glTF models, document attachments).

To restore locally: stop the backend, delete the existing `data/luminavault.db` and `data/uploads/`, unzip the backup into `LuminaVault-Backend/data/`, and restart. In Docker, restore into the path mounted as `LUMINA_DATA_DIR`.

For routine backups, point your favourite scheduler at this endpoint:

```bash
# Daily backup with auth header
curl -sH "Authorization: Bearer $LUMINA_TOKEN" \
  http://localhost:5256/api/data/backup \
  -o "backup-$(date +%Y%m%d).zip"
```

Or enable built-in scheduled backups:

```jsonc
{
  "Backup": {
    "Enabled": true,
    "Directory": "backups",
    "IntervalHours": 24,
    "RetainedFiles": 14
  }
}
```

### Category rules

Use **Settings → Category rules** to create simple auto-categorization rules. Rules run in priority order and only fill placeholder categories such as `General`, `Imported`, blank or `Uncategorized`; explicit categories are left alone. They apply to manual transaction saves and bank CSV imports.

### Exchange rates

Use **Settings → Exchange rates** to add dated manual conversion rates into CHF. Aggregate dashboard and statistics totals convert account balances, subscriptions, monthly summaries and transaction analytics into CHF. Transaction and monthly-summary analytics use the rate effective on that date, falling back to the nearest known rate when a currency has no older entry. Individual account and transaction rows still display in their original currency.

### Subscription automation

Use **Subscriptions → Forecast due** to generate pending forecast transactions for active, auto-renewing subscriptions due in the next seven days. The automation advances `NextDueOn` and tags generated transactions so repeat runs do not duplicate the same due date.

Scheduled generation is disabled by default. Enable it from backend configuration:

```jsonc
// LuminaVault-Backend/appsettings.json
{
  "SubscriptionAutomation": {
    "Enabled": true,
    "LookAheadDays": 7,
    "IntervalHours": 24
  }
}
```

### Notifications

A background `NotificationScanner` runs every few hours and writes rows into a `Notifications` table for three rule types:

- **SubscriptionDue** — any active auto-renewing subscription due within `SubscriptionDueLookAheadDays` (default 3)
- **BudgetOverrun** — any current-month budget where actual spend > limit
- **ForecastNegative** — the `ForecastLookAheadDays` (default 30) cash-flow projection dipping below zero

Each notification has a stable `Source` key (e.g. `subscription:42:due:2026-05-20`) with a unique index, so re-runs upsert instead of spamming. A user-dismissed notification will resurface on a later scan if the underlying condition still holds — the alert isn't permanently silenced just because it was acknowledged once. The sidebar bell shows the unread count and refreshes on every navigation. The `/notifications` page has unread/all/dismissed filters and a manual **Rescan** button. Defaults can be tuned:

```jsonc
{
  "Notifications": {
    "Enabled": true,
    "IntervalHours": 6,
    "SubscriptionDueLookAheadDays": 3,
    "ForecastLookAheadDays": 30
  }
}
```

### Cash-flow forecast

`/forecast` projects daily liquid cash forward for 14/30/60/90/180 days by replaying every cash movement we already know about — pending transactions plus scheduled subscription dues (rolled forward by their billing interval). It deliberately avoids trend-from-history estimation so every dip on the chart maps to a specific row in the event timeline.

The summary cards call out today's balance, projected ending balance, lowest point + date, and event count. A red banner appears if the projection crosses zero. Filter by account to drill into a single ledger. Subscription dues that already have a tagged pending transaction (from the auto-forecast job) are deduplicated to avoid double-counting.

### Net worth history

A `NetWorthBackgroundService` captures one snapshot per day (cash across accounts + holdings market value + inventory, all in the base currency) into the `NetWorthSnapshots` table. The dashboard renders the history as an SVG line chart with a delta badge vs. the earliest point. A "Snapshot now" button on the dashboard (or `POST /api/finance/net-worth/snapshot`) seeds the curve immediately. Defaults can be tuned:

```jsonc
{
  "NetWorth": {
    "Enabled": true,
    "IntervalHours": 24,
    "RetentionDays": 0      // 0 = keep forever
  }
}
```

### Bank CSV import

Use **Import & export → Import bank CSV** for bank statement files. The importer supports either:

- a signed `Amount` column, where negative rows become expenses and positive rows become income
- separate `Debit` and `Credit` columns

During import, LuminaVault checks existing transactions in the destination account by date, kind, payee and rounded amount, then skips likely duplicates. It accepts common date formats such as `yyyy-MM-dd`, `dd.MM.yyyy`, `dd/MM/yyyy`, and common decimal styles such as `42.50`, `42,50`, and `1'234.50`.

<br>

## Project structure

```
LuminaVault/
├── LuminaVault-Backend/
│   ├── Features/                       # Vertical slices — one folder per resource
│   │   ├── Auth/                       # User entity + JWT service + auth endpoints
│   │   ├── Inventory/
│   │   │   ├── Houses/                 # House entity + endpoints
│   │   │   ├── Rooms/
│   │   │   ├── Furniture/              # Furniture + Container
│   │   │   ├── Items/
│   │   │   ├── Photos/                 # ItemPhoto + photo upload
│   │   │   └── Attachments/            # DocumentAttachment (cross-cuts to Subscriptions)
│   │   ├── Finance/
│   │   │   ├── FinanceEndpoints.cs     # Orchestrator: calls each per-resource Map()
│   │   │   ├── Accounts/
│   │   │   ├── Transactions/
│   │   │   ├── Holdings/
│   │   │   │   └── Pricing/            # IPriceProvider + Finnhub & CoinGecko adapters
│   │   │   ├── Budgets/
│   │   │   ├── Goals/                  # Savings goals
│   │   │   ├── Subscriptions/          # Subscription entity + background auto-forecast service
│   │   │   ├── MonthlySummaries/
│   │   │   ├── BalanceSnapshots/
│   │   │   ├── NetWorth/               # Daily net-worth snapshots + history endpoint
│   │   │   ├── Forecast/               # Cash-flow projection (subscriptions + pending tx)
│   │   │   ├── Summary/                # /summary and /statistics aggregates
│   │   │   └── Shared/                 # FinanceHelpers (balance recompute), DTOs, mappers
│   │   ├── Data/
│   │   │   ├── Backup/                 # Backup endpoint + background scheduler
│   │   │   ├── BankCsv/                # Bank CSV preview + import
│   │   │   └── Ods/                    # ODS import/export pipeline
│   │   ├── Notifications/              # In-app alerts (scanner + background service + dedup)
│   │   └── Settings/                   # AssetCategory + FinanceCategory + ExchangeRate + CategoryRule
│   ├── Infrastructure/
│   │   ├── Data/                       # AppDbContext + Seeder + Migrations/
│   │   └── Validation/                 # Problem + Validate helpers
│   ├── data/                           # SQLite DB, uploads, logs and backups (gitignored)
│   └── Program.cs                      # App bootstrap, migrations & middleware
│
├── LuminaVault-Backend.Tests/          # xUnit + WebApplicationFactory integration tests
│
└── LuminaVault-Frontend/   # Angular app
    └── src/app/
        ├── core/           # Focused data-access services (auth/inventory/finance/etc.)
        ├── layout/         # App shell
        ├── shared/         # Reusable filters, confirm dialog
        └── pages/
            ├── dashboard/
            ├── login/
            ├── rooms/              # Room & furniture management
            ├── items/              # Item list & form
            ├── planner/            # Three.js scene + furniture models
            ├── accounts/           # Finance accounts
            ├── transactions/
            ├── holdings/           # Investment & crypto positions, price refresh
            ├── budgets/
            ├── goals/              # Savings goals
            ├── forecast/           # Cash-flow projection chart + event timeline
            ├── notifications/      # In-app alerts inbox
            ├── subscriptions/
            ├── monthly-summaries/  # Reconciliation
            ├── statistics/
            ├── data/               # ODS import/export
            └── settings/
```

The backend uses **vertical-slice architecture**: each feature folder owns its entity, endpoints, and DTOs together. Entity classes deliberately keep the flat `LuminaVault.Domain` namespace (folder location signals the bounded context), so EF Core's model snapshot is stable across refactors.

<br>

## Configuration

The backend reads JWT settings from `appsettings.json`. For development the key falls back to an environment variable `LUMINA_JWT_KEY`, or a hardcoded dev-only key if neither is set. Outside Development, the app refuses to start without a real 32+ byte signing key. First-user registration in non-development also requires `LUMINA_SETUP_SECRET` (or `Setup:RegistrationSecret`) so a fresh public instance cannot be claimed by a stranger.

```jsonc
// LuminaVault-Backend/appsettings.json
{
  "Jwt": {
    "Key": "your-secret-key-at-least-32-characters-long",
    "Issuer": "LuminaVault",
    "Audience": "LuminaVault"
  }
}
```

Maximum request body size is 50 MB (configured in `Program.cs`) to accommodate larger glTF models and document uploads.

### Running behind a reverse proxy

When LuminaVault sits behind a reverse proxy (the bundled nginx, or any TLS terminator), the backend's direct connection IP is the proxy's, not the client's — which would collapse the per-IP login rate limiter into a single shared bucket and mislabel request logs. Set `LUMINA_BEHIND_PROXY=true` (or `ForwardedHeaders:Enabled` in config) so the backend reads `X-Forwarded-For`/`X-Forwarded-Proto` and partitions throttling per real client. The bundled `docker-compose.yml` already sets this for the nginx-fronted deployment. **Leave it off for a directly-exposed backend** — trusting forwarded headers from an untrusted caller would let them spoof their address to dodge the rate limiter.

Hardening headers ship on every response: the API sends a strict `default-src 'none'` CSP (it only ever returns JSON or file bytes), and nginx sends the SPA a CSP that forbids inline/eval scripts and allows **no external origins** — every script, connection and worker is same-origin. The receipt-OCR engine (tesseract.js) is fully self-hosted under `/tesseract/`: its worker and WASM core are copied from `node_modules` at build time, and the English language pack (`eng.traineddata.gz`, ~3 MB) is committed under `LuminaVault-Frontend/public/tesseract/lang/`. The remaining CSP relaxations are local runtime needs only — `'wasm-unsafe-eval'` for the OCR core, `'unsafe-inline'` styles for Angular/PrimeNG, and `blob:` for object URLs.

### Two-factor authentication

Enable TOTP from **Settings → Security → Two-factor authentication**: scan the QR with any authenticator app (or type the shown secret), confirm a 6-digit code, then save the 10 one-time recovery codes that appear (each works once if you lose the device). Once enabled, sign-in is a two-step flow — password first, then the 6-digit code (or a recovery code). Disabling it re-checks your password. The TOTP scheme is RFC 6238 (SHA-1, 6 digits, 30s) and is implemented in-house with no third-party OTP dependency. If you're locked out and have no recovery codes left, a self-hosted operator can clear the `TwoFactorEnabled`/`TwoFactorSecret` columns on the single `Users` row directly.

The `/api/auth/login` endpoint is throttled to 10 attempts per minute per client IP — well above a human mistyping a password, but enough to blunt brute force. Tune it (e.g. to loosen for a shared egress IP) via config:

```jsonc
// LuminaVault-Backend/appsettings.json
{
  "RateLimiting": {
    "LoginPermitLimit": 10
  }
}
```

### Price providers (optional)

The Holdings page can pull live quotes from external providers. Both are optional — if a key is missing, the provider is reported as "not configured" and refreshes for that asset class are skipped with a clear message.

```jsonc
// LuminaVault-Backend/appsettings.json
{
  "PriceProviders": {
    "Finnhub": { "ApiKey": "your-finnhub-key" }
  }
}
```

Or via environment variable: `LUMINA_FINNHUB_KEY`.

- **Finnhub** (stocks / ETFs) — free tier allows ~60 calls/min. For non-US tickers, set each holding's *Provider ID* with the exchange suffix (e.g. `NESN.SW` for Swiss, `SAP.DE` for German).
- **CoinGecko** (crypto) — no API key required. For ambiguous tickers, set *Provider ID* to the exact CoinGecko coin id (e.g. `bitcoin`, `ethereum`).

The `POST /api/finance/holdings/refresh-prices` endpoint is rate-limited per JWT subject (6 calls/minute, fixed window) to prevent burning through Finnhub's free quota with rapid clicks; the 7th call within a minute returns `429 Too Many Requests`.

<br>

## Custom 3D models per furniture kind

Drop a glTF binary into the backend's working directory:

```
LuminaVault-Backend/models/Cabinet.glb
LuminaVault-Backend/models/Sofa.glb
...
```

The planner fetches `/models/{Kind}.glb` at runtime and falls back to the built-in procedural geometry if the file doesn't exist.

<br>

<div align="center">

Made with ☕, Three.js and a slightly obsessive spreadsheet habit

</div>
