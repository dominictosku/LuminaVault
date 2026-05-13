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
- **Transactions** — income / expense / transfer with categories, tags, payee, status (pending / cleared / reconciled)
- **Budgets** — per-category monthly limits with spend tracking
- **Recurring subscriptions** — billing interval, next-due date, auto-renew, optional document attachments (contracts, invoices)
- **Monthly summaries & reconciliation** — per-account opening/closing balances, income/expense totals, reconciliation notes
- **Balance snapshots** — record actual vs. expected balance over time to catch drift
- **Statistics** — charts and breakdowns for spend, income and category trends
- **ODS import/export** — round-trip your data with LibreOffice Calc spreadsheets (with a preview step before import)

### 🔐 Platform
- **Single-user JWT auth** — stateless, BCrypt-hashed password, token stored in localStorage
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

The SQLite database (`luminavault.db`) and an `uploads/` folder are created automatically on first run. Schema migrations run idempotently at startup — no CLI commands needed. Default asset and finance categories are seeded the first time the app starts.

### Frontend

```bash
cd LuminaVault-Frontend
npm install
npm start
# App available at http://localhost:4200
```

<br>

## Project structure

```
LuminaVault/
├── LuminaVault-Backend/
│   ├── Auth/               # JWT service
│   ├── Data/               # EF Core DbContext
│   ├── Domain/             # Entity classes (inventory + finance)
│   ├── Endpoints/          # Minimal API route groups
│   │   ├── HouseEndpoints.cs
│   │   ├── FurnitureEndpoints.cs
│   │   ├── ItemEndpoints.cs
│   │   ├── PhotoEndpoints.cs
│   │   ├── AttachmentEndpoints.cs
│   │   ├── FinanceEndpoints.cs       # accounts, transactions, budgets, subscriptions, snapshots
│   │   ├── OdsEndpoints.cs           # ODS import/export
│   │   ├── SettingsEndpoints.cs
│   │   └── AuthEndpoints.cs
│   ├── uploads/            # Photos, glTF models and documents (gitignored)
│   └── Program.cs          # App bootstrap, migrations & middleware
│
└── LuminaVault-Frontend/   # Angular app
    └── src/app/
        ├── core/           # Api service, models, auth guard & interceptor
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
            ├── budgets/
            ├── subscriptions/
            ├── monthly-summaries/  # Reconciliation
            ├── statistics/
            ├── data/               # ODS import/export
            └── settings/
```

<br>

## Configuration

The backend reads JWT settings from `appsettings.json`. For development the key falls back to an environment variable `LUMINA_JWT_KEY`, or a hardcoded dev-only key if neither is set.

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
