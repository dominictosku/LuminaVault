import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
  API_BASE,
  SERVER_URL_STORAGE_KEY,
  getConfiguredServerUrl,
  setConfiguredServerUrl,
  isTauriEnvironment,
} from './api-base';

describe('api-base', () => {
  beforeEach(() => {
    localStorage.clear();
    delete (window as any).__TAURI_INTERNALS__;
    delete (window as any).__TAURI__;
  });

  afterEach(() => {
    localStorage.clear();
    delete (window as any).__TAURI_INTERNALS__;
    delete (window as any).__TAURI__;
  });

  it('detects tauri environment when __TAURI_INTERNALS__ or __TAURI__ is set', () => {
    expect(isTauriEnvironment()).toBe(false);

    (window as any).__TAURI_INTERNALS__ = {};
    expect(isTauriEnvironment()).toBe(true);

    delete (window as any).__TAURI_INTERNALS__;
    (window as any).__TAURI__ = {};
    expect(isTauriEnvironment()).toBe(true);
  });

  it('defaults to empty string in standard web browser when no stored url', () => {
    expect(getConfiguredServerUrl()).toBe('');
    expect(`${API_BASE}/api/items`).toBe('/api/items');
  });

  it('defaults to http://localhost:5256 in Tauri desktop when no stored url', () => {
    (window as any).__TAURI_INTERNALS__ = {};
    expect(getConfiguredServerUrl()).toBe('http://localhost:5256');
    expect(`${API_BASE}/api/items`).toBe('http://localhost:5256/api/items');
  });

  it('uses explicitly configured server URL from localStorage and strips trailing slash', () => {
    setConfiguredServerUrl('https://vault.myhome.net:8443///');
    expect(localStorage.getItem(SERVER_URL_STORAGE_KEY)).toBe('https://vault.myhome.net:8443');
    expect(getConfiguredServerUrl()).toBe('https://vault.myhome.net:8443');
    expect(`${API_BASE}/api/auth/login`).toBe('https://vault.myhome.net:8443/api/auth/login');
  });

  it('clears stored URL when setConfiguredServerUrl is called with empty string', () => {
    setConfiguredServerUrl('https://vault.myhome.net');
    expect(getConfiguredServerUrl()).toBe('https://vault.myhome.net');

    setConfiguredServerUrl('');
    expect(localStorage.getItem(SERVER_URL_STORAGE_KEY)).toBeNull();
    expect(getConfiguredServerUrl()).toBe('');
  });

  it('supports string concatenation and methods on API_BASE', () => {
    setConfiguredServerUrl('http://192.168.1.100:5256');
    expect(API_BASE + '/api/data').toBe('http://192.168.1.100:5256/api/data');
    expect(API_BASE.startsWith('http')).toBe(true);
  });
});
