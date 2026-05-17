// Empty base means every request hits the same origin as the SPA — the production
// path, where nginx (or any reverse proxy) routes /api/* to the backend container.
// In `ng serve`, proxy.conf.json forwards /api/* to http://localhost:5256 instead,
// so dev keeps working without code changes.
export const API_BASE = '';
