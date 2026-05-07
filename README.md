<div align="center">

# ✦ LuminaVault

**A beautiful home inventory app with an interactive 3D planner**

[![Stars](https://img.shields.io/github/stars/dominictosku/LuminaVault?style=for-the-badge&color=7c3aed&labelColor=0f0f1a)](https://github.com/dominictosku/LuminaVault/stargazers)
[![Forks](https://img.shields.io/github/forks/dominictosku/LuminaVault?style=for-the-badge&color=6d28d9&labelColor=0f0f1a)](https://github.com/dominictosku/LuminaVault/network/members)
[![Issues](https://img.shields.io/github/issues/dominictosku/LuminaVault?style=for-the-badge&color=a78bfa&labelColor=0f0f1a)](https://github.com/dominictosku/LuminaVault/issues)
[![License](https://img.shields.io/github/license/dominictosku/LuminaVault?style=for-the-badge&color=8b5cf6&labelColor=0f0f1a)](LICENSE)

[![Angular](https://img.shields.io/badge/Angular-21-DD0031?style=flat-square&logo=angular)](https://angular.dev)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![Three.js](https://img.shields.io/badge/Three.js-r170-000000?style=flat-square&logo=threedotjs)](https://threejs.org)
[![TailwindCSS](https://img.shields.io/badge/Tailwind-v4-06B6D4?style=flat-square&logo=tailwindcss)](https://tailwindcss.com)

</div>

---

> Track every item in your home — from the living-room cabinet to the left shelf of the hallway wardrobe — and explore it all in a fluid, animated 3D walkthrough.

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

- **House → Room → Furniture → Container → Item** hierarchy — place anything anywhere
- **3D planner** — animated Three.js walkthrough with GSAP; click rooms to fly inside, click furniture to inspect it
- **Per-kind 3D models** — procedurally generated geometry for cabinets, shelves, wardrobes, beds, sofas and more; falls back to custom glTF if a `/models/{Kind}.glb` exists
- **Per-item glTF upload** — attach a `.glb` or `.gltf` model to any item and see it floating in the 3D view
- **Photo gallery** — upload multiple photos per item
- **Glassmorphism UI** — dark, frosted-glass design with smooth transitions
- **Single-user JWT auth** — secure, stateless, token stored in localStorage
- **Full-text search** — filter items by name, brand, model, serial number or tags
- **Stats dashboard** — total value, items per room, recently added

<br>

## Tech stack

| Layer | Technology |
|---|---|
| Frontend | Angular 21 · Standalone components · Signals · Zoneless CD |
| Styling | Tailwind CSS v4 · PrimeIcons · Glassmorphism |
| 3D | Three.js · OrbitControls · GLTFLoader · GSAP |
| Backend | ASP.NET Core 10 · Minimal APIs · EF Core 9 · SQLite |
| Auth | JWT Bearer · BCrypt.Net |

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

The SQLite database (`luminavault.db`) and an `uploads/` folder are created automatically on first run. Schema migrations run idempotently at startup — no CLI commands needed.

### Frontend

```bash
cd mobile
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
│   ├── Domain/             # Entity classes
│   ├── Endpoints/          # Minimal API route groups
│   └── Program.cs          # App bootstrap & middleware
│
└── mobile/                 # Angular app
    └── src/app/
        ├── core/           # Api service, models, auth
        └── pages/
            ├── dashboard/
            ├── items/      # Item list & form
            ├── planner/    # Three.js scene + furniture models
            └── rooms/      # Room & furniture management
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

Made with ☕ and Three.js

</div>
