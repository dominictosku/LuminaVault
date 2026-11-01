<div align="center">

# ✦ LuminaVault

<p align="center">
  <strong>Your personal vault for everything you own and everything you spend.</strong>
  <br />
  A self-hosted home inventory with an interactive 3D planner, paired with a full personal-finance suite.
</p>

<p align="center">
  <img alt="Personal learning project" src="https://img.shields.io/badge/Status-Personal_Learning_Project-7c3aed?style=for-the-badge&labelColor=0f0f1a">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/License-MIT-8b5cf6?style=for-the-badge&labelColor=0f0f1a"></a>
</p>

<p align="center">
  <a href="#-features">Features</a> •
  <a href="#-quick-start">Quick Start</a> •
  <a href="#-tech-stack">Tech Stack</a> •
  <a href="#-documentation">Documentation</a> •
  <a href="#-license">License</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Angular_21-DD0031?style=flat-square&logo=angular&logoColor=white" alt="Angular 21">
  <img src="https://img.shields.io/badge/.NET_10-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/Three.js-r171-black?style=flat-square&logo=threedotjs&logoColor=white" alt="Three.js">
  <img src="https://img.shields.io/badge/Tailwind_CSS_v4-06B6D4?style=flat-square&logo=tailwindcss&logoColor=white" alt="Tailwind CSS v4">
  <img src="https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite&logoColor=white" alt="SQLite">
  <img src="https://img.shields.io/badge/PrimeNG_21-007ad9?style=flat-square" alt="PrimeNG 21">
</p>

</div>

---

> Track every item in your home — from the living-room cabinet to the left shelf of the hallway wardrobe — *and* every franc that moves through your accounts. One app, one self-hosted database, one dark glassmorphism UI.

<br>

## 📸 Screenshots

<div align="center">

| Overview Dashboard | Interactive 3D Planner | Asset Details & Battery Tracking |
|:---:|:---:|:---:|
| ![Dashboard screenshot](docs/screenshots/dashboard.png) | ![3D planner screenshot](docs/screenshots/planner.png) | ![Item form screenshot](docs/screenshots/item-form.png) |

<sub>*Place your own screenshots in `docs/screenshots/` to replace the placeholders.*</sub>

</div>

<br>

## ✦ Features

### 🏠 Home Inventory & 3D Spatial Planner
- **5-Level Hierarchy**: Organize by `House → Room → Furniture → Container → Item`.
- **Interactive 3D Planner**: Animated Three.js walkthrough with smooth GSAP camera navigation. Click rooms to enter, click furniture to slide drawers and open cabinet doors.
- **Custom & Procedural 3D Models**: Built-in procedural models for all common furniture types; upload per-item `.glb`/`.gltf` models to float in the 3D scene or provide custom furniture models.
- **⚡ Lithium Battery Maintenance Check**:
  - Specify if an item has a lithium battery with a periodic check interval (default: **every 3 months**).
  - Automated alerts warn when a battery recharge is due to prevent irreversible cell degradation.
  - Distinct **amber attention styling**, warning glow, badge indicators, and 3D scene orb color differentiation.
  - One-click **"Mark charged"** action from list cards and item details.
- **Photos & Attachments**: Upload multiple photos, receipts, manuals, and warranty documents per item.

### 💰 Personal Finance Suite
- **Multi-Account Ledger**: Checking, savings, investments, crypto, cash, credit cards, and loans in their native currencies with dated exchange rate conversions to CHF.
- **Transactions & Multi-Category Splits**: Income, Expense, Transfer, and Trade events (Buy/Sell/Dividend/Fee). Split single receipts across multiple categories.
- **Automated Category Rules**: Pattern-based rules to auto-categorize statement imports and transactions.
- **Investment & Crypto Holdings**: Track average cost basis, market value, unrealized P&L, allocation, and refresh live quotes via **Finnhub** and **CoinGecko**.
- **Budgets & Savings Goals**: Monthly per-category spend limits with progress tracking and savings target monitors.
- **Loan Amortisation**: Full mortgage and loan amortization schedules showing monthly interest/principal splits and payoff accelerators.
- **Recurring Subscriptions & Cash Forecast**: Calendar-aware billing cycles with automatic pending forecast creation and forward cash-flow projection (14–180 days).
- **Daily Net Worth History**: Automated daily snapshots aggregating cash, investments, and inventory assets.
- **Swiss Tax Export**: Generate year-end wealth declarations and Wertschriftenverzeichnis bundles.

### 🔐 Security & Platform
- **Self-Hosted Privacy**: Single-file SQLite database (`luminavault.db`). Zero telemetry, zero external dependencies.
- **Stateless Single-User JWT Auth**: Registration automatically locks permanently after the first user is created.
- **Two-Factor Authentication (TOTP)**: RFC 6238 authenticator app support with QR enrollment and 10 one-time recovery codes.
- **Hardened Security**: Strict same-origin CSP, brute-force login throttling, and fully self-hosted, offline Tesseract OCR.
- **Modern UI**: Dark glassmorphism interface built with Angular 21 (Zoneless + Signals), PrimeNG, and Tailwind CSS v4.

<br>

## 📚 Documentation

Detailed documentation has been organized into dedicated guides in the [`docs/`](docs/) directory:

| Guide | Description |
|---|---|
| [🏠 **Home Inventory & 3D Planner**](docs/inventory.md) | Spatial hierarchy, 3D planner walkthrough, custom glTF models, and lithium battery maintenance tracking. |
| [💰 **Personal Finance Suite**](docs/finance.md) | Multi-currency ledger, transaction splits, holdings, live quotes, budgets, loans, forecast, and tax exports. |
| [🔔 **Notifications & Automated Scanner**](docs/notifications.md) | Background scanner, deduplication keys, alert lifecycle, and notification configuration. |
| [📦 **Data Management & Backups**](docs/data-and-backups.md) | ODS spreadsheet round-trips, bank CSV statement importing, and disaster recovery snapshots. |
| [🔐 **Security, Auth & Configuration**](docs/security-and-auth.md) | JWT setup, TOTP 2FA, rate limits, reverse proxy setup, CSP headers, and environment variables. |
| [🏛️ **Architecture & Project Structure**](docs/architecture.md) | Vertical-slice backend design, zoneless Angular frontend, 3D graphics pipeline, and source tree. |

<br>

## 🛠 Tech Stack

| Layer | Technology |
|---|---|
| **Frontend** | Angular 21 (Zoneless, Signals, Standalone Components) |
| **UI & Styling** | PrimeNG 21 · Tailwind CSS v4 · PrimeIcons · Frosted Glassmorphism |
| **3D Engine** | Three.js r171 · OrbitControls · GLTFLoader · GSAP Animation Suite |
| **Backend** | ASP.NET Core 10 (Minimal APIs, Vertical Slice Architecture) |
| **ORM & Database** | Entity Framework Core 9/10 · SQLite with WAL mode & automated migrations |
| **Auth & Security** | JWT Bearer · Salted BCrypt · RFC 6238 TOTP 2FA |
| **Document OCR** | Tesseract.js (Self-hosted offline WebAssembly & language models) |
| **Data Interop** | OpenDocument Spreadsheet (`.ods`) · Bank CSV formats |

<br>

## 🚀 Quick Start

### Option 1: Docker Compose (Recommended)

The fastest way to deploy LuminaVault for self-hosting:

```bash
# 1. Clone repository
git clone https://github.com/dominictosku/LuminaVault.git
cd LuminaVault

# 2. Configure environment
cp .env.example .env

# Generate signing keys:
openssl rand -base64 48 | tr -d '\n' > /tmp/k && echo LUMINA_JWT_KEY=$(cat /tmp/k) >> .env
openssl rand -base64 32 | tr -d '\n' > /tmp/s && echo LUMINA_SETUP_SECRET=$(cat /tmp/s) >> .env

# 3. Start stack
docker compose up -d --build
```

Access the app at **http://localhost:8080**.

---

### Option 2: Local Development

#### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org)

#### 1. Backend
```bash
cd LuminaVault-Backend
dotnet run
# API runs at http://localhost:5256
```
*The SQLite database, storage folders, and default categories are created and migrated automatically on first start.*

#### 2. Frontend
```bash
cd LuminaVault-Frontend
npm install
npm start
# App runs at http://localhost:4200
```

---

### 🧪 Running Tests

Both backend and frontend feature thorough test suites:

```bash
# Run backend tests (integration, domain math, migrations, notifications):
dotnet test LuminaVault-Backend.Tests/LuminaVault-Backend.Tests.csproj

# Run frontend tests (vitest unit tests, battery logic, OCR):
npm --prefix LuminaVault-Frontend test
```

<br>

## 📄 License

LuminaVault's original code is licensed under the [MIT License](LICENSE).  
Third-party dependencies and bundled assets retain their respective licenses. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for complete attributions.

<br>

<div align="center">
  <sub>Built with ☕, Three.js, and a passion for clean software.</sub>
</div>
