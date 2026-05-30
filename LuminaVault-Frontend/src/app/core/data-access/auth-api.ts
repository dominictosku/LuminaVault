import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  AuthResponse,
  AuthStatus,
  TwoFactorEnableResult,
  TwoFactorSetup,
  TwoFactorStatus,
} from '../models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private http = inject(HttpClient);

  authStatus() {
    return this.http.get<AuthStatus>(`${API_BASE}/api/auth/status`);
  }

  login(username: string, password: string, totpCode?: string, recoveryCode?: string) {
    return this.http.post<AuthResponse>(`${API_BASE}/api/auth/login`, {
      username,
      password,
      totpCode: totpCode || null,
      recoveryCode: recoveryCode || null,
    });
  }

  twoFactorStatus() {
    return this.http.get<TwoFactorStatus>(`${API_BASE}/api/auth/2fa/status`);
  }

  twoFactorSetup() {
    return this.http.post<TwoFactorSetup>(`${API_BASE}/api/auth/2fa/setup`, {});
  }

  twoFactorEnable(code: string) {
    return this.http.post<TwoFactorEnableResult>(`${API_BASE}/api/auth/2fa/enable`, { code });
  }

  twoFactorDisable(password: string) {
    return this.http.post<TwoFactorStatus>(`${API_BASE}/api/auth/2fa/disable`, { password });
  }

  register(username: string, password: string, setupSecret?: string) {
    return this.http.post<AuthResponse>(`${API_BASE}/api/auth/register`, {
      username,
      password,
      setupSecret: setupSecret || null,
    });
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.post<AuthResponse>(`${API_BASE}/api/auth/change-password`, {
      currentPassword,
      newPassword,
    });
  }
}
