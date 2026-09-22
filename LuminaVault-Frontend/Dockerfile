# --- Build stage: compile the Angular SPA against the production config ---
FROM node:22-alpine AS build
WORKDIR /src

# Copy only the package manifests first so npm install is cached across source edits.
COPY package.json package-lock.json* ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration=production

# --- Runtime stage: nginx serves the static bundle and proxies /api/* to the backend ---
FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/LuminaVault/browser /usr/share/nginx/html
# Angular writes extracted bundle notices beside browser/, not inside it.
COPY --from=build /src/dist/LuminaVault/3rdpartylicenses.txt /usr/share/nginx/html/3rdpartylicenses.txt

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget --spider -q http://127.0.0.1/ || exit 1
