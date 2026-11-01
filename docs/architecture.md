# 🏛️ Architecture & Project Structure

LuminaVault is architected as a lightweight, clean, decoupled mono-repository with a vertical-slice ASP.NET Core backend and a modern zoneless Angular frontend.

---

## 1. Backend Architecture

The backend follows a **Vertical Slice Architecture** where domain entities, Minimal API endpoints, database mappings, and DTOs live together grouped by business feature rather than technical layers.

```
LuminaVault-Backend/
├── Features/                       # Vertical slices — one folder per bounded context
│   ├── Auth/                       # User entity, BCrypt hasher, TOTP 2FA, JWT service
│   ├── Inventory/                  # Houses, Rooms, Furniture, Items, Photos, Attachments
│   ├── Finance/                    # Accounts, Transactions, Splits, Holdings, Budgets, Loans, Tax
│   ├── Notifications/              # Background service, scanner, notification rules
│   ├── Data/                       # Backup manager, Bank CSV parser, ODS import/export
│   ├── Settings/                   # Categories, Exchange rates, Categorization rules
│   └── System/                     # Health check endpoints
├── Infrastructure/
│   ├── Data/                       # AppDbContext, SQLite Pragma interceptor, EF Migrations
│   ├── Hosting/                    # Middleware pipelines, CORS, rate limits, timeouts
│   ├── Storage/                    # File upload persistence & path resolution
│   └── Validation/                 # ProblemDetails & request validation helpers
└── Program.cs                      # Composition root & endpoint mappings
```

### Domain Model Stability
Entities share the root `LuminaVault.Domain` namespace regardless of their folder location in `Features/`. This allows features to be moved, organized, or refactored without breaking EF Core migrations or model snapshots.

### EF Core Migrations
Migrations run automatically on boot via `app.MigrateAndSeedDatabase()`. When modifying domain entities:
```bash
cd LuminaVault-Backend
dotnet ef migrations add <MigrationName>
```

---

## 2. Frontend Architecture

The frontend is an **Angular 21** single-page application built with modern reactive principles:

- **Zoneless Change Detection**: Runs without `zone.js` for superior runtime performance and deterministic change triggers.
- **Signals**: State management is handled through Angular Signals (`signal`, `computed`, `effect`).
- **Standalone Components**: Modular architecture with no `NgModule` baggage.
- **Glassmorphism Design**: Frosted glass dark-mode aesthetic utilizing Tailwind CSS v4 and PrimeNG 21 components.

```
LuminaVault-Frontend/
└── src/app/
    ├── core/                       # Core APIs, models, auth interceptors & guards
    │   ├── data-access/            # Feature API services (HTTP client abstractions)
    │   └── models/                 # Domain DTO interfaces
    ├── layout/                     # App shell, navigation drawer, toast outlet
    ├── pages/                      # Page components & routed views
    │   ├── dashboard/              # Net worth summary, charts, quick actions
    │   ├── items/                  # Asset catalog, battery status, photo/model forms
    │   ├── planner/                # 3D Three.js room visualizer
    │   ├── accounts/               # Account ledger & balance tracking
    │   ├── transactions/           # Transaction ledger, split editor, categorization
    │   ├── holdings/               # Stock/crypto portfolio & live pricing
    │   ├── budgets/                # Monthly budgets & spend limits
    │   ├── forecast/               # Cash flow projection chart & timeline
    │   ├── subscriptions/          # Recurring services & interval calculations
    │   ├── notifications/          # In-app alert center
    │   └── settings/               # Categories, rules, exchange rates, 2FA
    └── shared/                     # Reusable modals, Sankey diagrams, confirmation dialogs
```

---

## 3. 3D Engine Architecture

The interactive home planner (`/planner`) renders directly in the browser via **Three.js**:

- **Scene Graph**: Houses contain rooms; rooms contain furniture meshes; furniture contains item markers.
- **GSAP Tweens**: Camera movements, drawer open/close animations, and floating item bobbing are orchestrated with GSAP easing curves.
- **Marker Color Pipeline**:
  - Regular furniture items: Glowing violet orb (`#a78bfa`).
  - Loose room items: Glowing pink orb (`#ff6fb3`).
  - Battery charge overdue items: Glowing amber orb (`#f59e0b`) with elevated emissive intensity.
  - Uploaded 3D models: Fetched on demand via authenticated JWT endpoints and rendered in place of placeholder orbs.
