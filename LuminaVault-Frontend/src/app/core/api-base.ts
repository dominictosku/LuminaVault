// Stored server URL key in localStorage.
export const SERVER_URL_STORAGE_KEY = 'luminavault_server_url';

/**
 * Checks whether the application is running inside the Tauri v2 desktop environment.
 */
export function isTauriEnvironment(): boolean {
  return (
    typeof window !== 'undefined' &&
    ('__TAURI_INTERNALS__' in window || '__TAURI__' in window)
  );
}

/**
 * Gets the configured backend server URL.
 * - If a custom URL has been set in localStorage, returns that (stripped of trailing slashes).
 * - If running in Tauri desktop without an explicit URL, defaults to 'http://localhost:5256'.
 * - In web/browser mode without an override, returns '' (empty string for same-origin proxy).
 */
export function getConfiguredServerUrl(): string {
  if (typeof window === 'undefined') return '';
  const stored = localStorage.getItem(SERVER_URL_STORAGE_KEY);
  if (stored && stored.trim()) {
    return stored.trim().replace(/\/+$/, '');
  }
  if (isTauriEnvironment()) {
    return 'http://localhost:5256';
  }
  return '';
}

/**
 * Persists or clears the backend server URL.
 * Passing an empty string removes the custom override.
 */
export function setConfiguredServerUrl(url: string): void {
  if (typeof window === 'undefined') return;
  const clean = url.trim().replace(/\/+$/, '');
  if (!clean) {
    localStorage.removeItem(SERVER_URL_STORAGE_KEY);
  } else {
    localStorage.setItem(SERVER_URL_STORAGE_KEY, clean);
  }
}

/**
 * Tests connection to a LuminaVault server endpoint.
 */
export async function testServerConnection(url?: string): Promise<{
  success: boolean;
  message: string;
  hasUser?: boolean;
}> {
  const target = (url !== undefined ? url : getConfiguredServerUrl()).trim().replace(/\/+$/, '');
  const baseUrl = target || (typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5256');

  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 6000);

  try {
    const res = await fetch(`${baseUrl}/api/auth/status`, {
      method: 'GET',
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    });
    clearTimeout(timeoutId);

    if (res.ok) {
      const data = await res.json().catch(() => ({}));
      return {
        success: true,
        message: 'Connected successfully to LuminaVault server.',
        hasUser: typeof data?.hasUser === 'boolean' ? data.hasUser : undefined,
      };
    }
    return {
      success: false,
      message: `Server returned status ${res.status}: ${res.statusText}`,
    };
  } catch (err: any) {
    clearTimeout(timeoutId);
    if (err?.name === 'AbortError') {
      return { success: false, message: 'Connection timed out after 6 seconds.' };
    }
    return {
      success: false,
      message: err?.message || 'Could not reach server. Check the address and ensure the server is running.',
    };
  }
}

const apiBaseTarget = {
  toString: () => getConfiguredServerUrl(),
  valueOf: () => getConfiguredServerUrl(),
  [Symbol.toPrimitive]: () => getConfiguredServerUrl(),
};

/**
 * Dynamic API base URL:
 * Seamlessly resolves to the configured server URL in template literals (`${API_BASE}/api/...`),
 * string concatenations, or direct access.
 */
export const API_BASE: string = new Proxy(apiBaseTarget, {
  get(target, prop, receiver) {
    if (prop in target) {
      return Reflect.get(target, prop, receiver);
    }
    const current = getConfiguredServerUrl();
    const val = (current as any)[prop];
    if (typeof val === 'function') {
      return val.bind(current);
    }
    return val;
  },
}) as unknown as string;
