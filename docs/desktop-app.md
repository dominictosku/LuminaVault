# LuminaVault Desktop App

LuminaVault can be built and run as a lightweight native desktop application powered by [Tauri v2](https://v2.tauri.app/) and the modern Angular frontend. The desktop client connects directly to your self-hosted LuminaVault backend server over REST API, giving you the feel and convenience of a native desktop application with full data synchronization to your private server.

---

## ⚡ Highlights

- **Ultra-lightweight footprint:** Uses the native web engine (WebKitGTK on Linux, WebKit on macOS, WebView2 on Windows) — typically requiring only ~40–60 MB RAM (compared to ~350 MB+ for Electron).
- **Dynamic Server URL:** Connect to a local backend on your workstation or to a remote self-hosted server on your NAS or homelab.
- **Connection Switcher:** Check connectivity, latency, and change backend addresses directly from the Sign-In screen or the Settings page.
- **Native OS Notifications:** Receive system desktop alerts for battery charge maintenance schedules and upcoming subscription billing dates.
- **Cross-Platform:** Builds native binaries and bundles for Linux (`.deb`, `.AppImage`), macOS (`.dmg`, `.app`), and Windows (`.msi`, `.exe`).

---

## 📐 Architecture & Synchronization

```
┌────────────────────────────────────────────────────────┐
│               LuminaVault Desktop App                  │
│                                                        │
│  ┌─────────────────────────┐  ┌─────────────────────┐  │
│  │   Angular Frontend      │  │ Tauri v2 Rust Host  │  │
│  │   - Dynamic API_BASE    │  │ - OS Notifications  │  │
│  │   - LocalStorage Config │  │ - Window Management │  │
│  │   - Offline resilience  │  │ - Webview Bridge    │  │
│  └───────────┬─────────────┘  └─────────────────────┘  │
└──────────────┼─────────────────────────────────────────┘
               │ REST API (Bearer JWT / CORS)
               ▼
┌────────────────────────────────────────────────────────┐
│            LuminaVault Backend Server                  │
│                                                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │ ASP.NET Core 10 Web API                          │  │
│  │ - CORS: tauri://localhost, custom origins        │  │
│  │ - SQLite (WAL mode)                              │  │
│  │ - Notification Scanner & Background Services     │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
```

- **Authentication:** Authenticates with the backend via Bearer JWT tokens stored securely in client storage.
- **CORS Handling:** The backend is configured to accept requests from Tauri desktop origins (`tauri://localhost`, `http://tauri.localhost`, `https://tauri.localhost`) as well as dev origins (`http://localhost:4200`).

---

## ⚙️ Prerequisites

To build or run the desktop app from source, you will need:

1. **Node.js** (v20+ recommended) and **npm**
2. **Rust & Cargo** (1.80+):
   ```bash
   curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
   ```
3. **OS System Libraries:**
   - **Linux (Debian/Ubuntu/Fedora/Arch):**
     - WebKit2GTK (`webkit2gtk-4.1` or `webkit2gtk4.1-devel`)
     - GTK 3 (`libgtk-3-dev` or `gtk3-devel`)
     - libsoup (`libsoup3` or `libsoup3-devel`)
   - **macOS:** Xcode command line tools (`xcode-select --install`)
   - **Windows:** Microsoft Visual Studio C++ Build Tools & Edge WebView2

---

## 🚀 Running in Development

1. Ensure the LuminaVault backend server is running:
   ```bash
   dotnet run --project LuminaVault-Backend
   ```
   *(By default, backend listens on `http://localhost:5256`)*

2. Start the Tauri development workflow:
   ```bash
   cd LuminaVault-Frontend
   npm run tauri:dev
   ```
   This will concurrently launch the Angular dev server and wrap it inside the Tauri native desktop window with hot reloading enabled.

---

## 📦 Building the Desktop Binary / Installer

### Building All Formats
To create an optimized production build for all configured bundles:
```bash
cd LuminaVault-Frontend
npm run tauri:build
```

### Building AppImage Specifically
To build a standalone portable Linux **AppImage**:
```bash
cd LuminaVault-Frontend
npm run tauri:appimage
```
*(Or `npx tauri build --bundles appimage`)*

The output `.AppImage` will be placed in:
```
LuminaVault-Frontend/src-tauri/target/release/bundle/appimage/LuminaVault_<version>_amd64.AppImage
```

#### Running the AppImage
Make the file executable and run it:
```bash
chmod +x LuminaVault_*_amd64.AppImage
./LuminaVault_*_amd64.AppImage
```

> [!TIP]
> If running on a system or container without FUSE support (such as certain Docker or minimal WSL setups), prefix execution with:
> ```bash
> APPIMAGE_EXTRACT_AND_RUN=1 ./LuminaVault_*_amd64.AppImage
> ```

### Building Debian Package (.deb)
To build a `.deb` package for Ubuntu/Debian:
```bash
cd LuminaVault-Frontend
npm run tauri:deb
```

---

## 🌐 Connecting to a Remote or Homelab Server

1. Open the LuminaVault desktop app.
2. At the sign-in screen, click **Change** on the backend status bar (or go to **Settings → Server connection** when signed in).
3. Enter the URL of your remote server (for example `http://192.168.1.100:5256` or `https://vault.myhome.net`).
4. Click **Test** to verify network reachability.
5. Click **Save** to apply the configuration.

### Allowing Custom Origins on Remote Backend
If your backend is running behind a custom domain or reverse proxy, configure the allowed origins in your backend environment:

```bash
# In docker-compose or container environment:
LUMINA_CORS_ORIGINS=https://vault.myhome.net,http://192.168.1.100:5256
```
*(Tauri desktop client origins `tauri://localhost` and `http://tauri.localhost` are always allowed automatically by LuminaVault backend).*

---

## 🔔 Native Desktop Notifications

LuminaVault Desktop supports OS-level desktop notifications for:
- **Lithium battery maintenance checks:** Alerts when an item with a lithium battery is due for a top-up charge.
- **Subscription renewals:** Alerts prior to recurring charge dates.
- **Critical warnings:** Negative balances or overdue payments.

You can test notification dispatch or grant permissions at any time under **Settings → Server connection → Desktop notifications**.
