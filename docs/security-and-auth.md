# 🔐 Security, Authentication & Configuration

LuminaVault is built from the ground up as a zero-telemetry, single-tenant, self-hosted vault.

---

## 1. Single-User JWT Authentication

LuminaVault is designed for a single user/household owner:

- **Stateless Tokens**: Signed with HMAC-SHA256 via `LUMINA_JWT_KEY` (minimum 32 characters).
- **First-Run Registration Lock**: The registration endpoint (`/api/auth/register`) is only active until the initial owner account is created. All subsequent calls return `409 Conflict`.
- **Production Setup Secret**: In non-development environments, initial registration requires `LUMINA_SETUP_SECRET` to prevent public instances from being claimed by unauthorized parties.
- **Password Hashing**: Passwords are saved with salted BCrypt hashes.

---

## 2. Two-Factor Authentication (TOTP)

Add an extra layer of protection using standard authenticator apps (1Password, Bitwarden, Google Authenticator, Authy):

- **Standard RFC 6238**: SHA-1 algorithm, 6 digits, 30-second window. Fully in-house implementation with zero third-party cloud dependencies.
- **Setup Flow**: Navigate to **Settings → Security → Two-factor authentication**, scan the QR code, verify a code, and securely save the **10 one-time recovery codes**.
- **Login Flow**: Submitting credentials triggers a second challenge prompting for a 6-digit TOTP code or an 8-character recovery code.
- **Admin Lockout Override**: As a self-hosted admin, if you lose your authenticator device and recovery codes, reset the 2FA state directly in SQLite:
  ```bash
  sqlite3 data/luminavault.db "UPDATE Users SET TwoFactorEnabled = 0, TwoFactorSecret = NULL, TwoFactorRecoveryCodes = NULL;"
  ```

---

## 3. Reverse Proxy & Forwarded Headers

When hosting behind reverse proxies (Nginx, Traefik, Caddy, Cloudflare Tunnel, NPM):
- **Enable Client IP Forwarding**: Set `LUMINA_BEHIND_PROXY=true` in `.env` or `docker-compose.yml`.
- **Why**: Allows ASP.NET Core to honor `X-Forwarded-For` and `X-Forwarded-Proto`, ensuring rate-limiting partitions against the real client IP rather than the proxy IP.
- **Security Rule**: Leave this option **disabled** if exposing the Kestrel backend port directly to the internet without a reverse proxy.

---

## 4. Response Headers & CSP Hardening

LuminaVault applies strict Content Security Policies on all responses:

- **API Layer**: Sends `default-src 'none'; frame-ancestors 'none'; base-uri 'none'`. The API never serves HTML or executable scripts.
- **Frontend Layer**: Served via Nginx with strict same-origin restrictions:
  - Disallows inline/eval scripts.
  - Forbids external connections or CDN origins.
- **Offline OCR Engine**: The receipt OCR system (**Tesseract.js**) is self-hosted:
  - Worker and WASM core are served locally under `/tesseract/`.
  - Trained language data (`eng.traineddata.gz`) is committed locally in `public/tesseract/lang/`.
  - Zero external calls are made during document OCR.

---

## 5. Rate Limiting

- **Brute-Force Login Protection**: Throttles `/api/auth/login` to **10 attempts per minute** per client IP. Configurable via `RateLimiting:LoginPermitLimit`.
- **Price Provider Quota Protection**: Calls to `/api/finance/holdings/refresh-prices` are throttled to **6 calls per minute** per user session to prevent exhausting external API rate limits.

---

## 6. Environment Variables Reference

| Variable | Description | Default |
|---|---|---|
| `LUMINA_JWT_KEY` | 32+ character signing key for JWT tokens | *(Required in Production)* |
| `LUMINA_SETUP_SECRET` | Secret required for first-user registration in production | *(Required outside Dev)* |
| `LUMINA_DATA_DIR` | Absolute path to persist DB, uploads, and logs | `./data` |
| `LUMINA_BEHIND_PROXY` | Trust `X-Forwarded-For` from reverse proxy | `false` (set `true` in Docker) |
| `LUMINA_FINNHUB_KEY` | Optional API key for stock/ETF quotes from Finnhub | `""` |
| `ASPNETCORE_ENVIRONMENT` | Environment mode (`Development` / `Production`) | `Production` |
