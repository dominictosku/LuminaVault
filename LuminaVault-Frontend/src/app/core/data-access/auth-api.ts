import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { AuthResponse, AuthStatus } from '../models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private http = inject(HttpClient);

  authStatus() {
    return this.http.get<AuthStatus>(`${API_BASE}/api/auth/status`);
  }

  login(username: string, password: string) {
    return this.http.post<AuthResponse>(`${API_BASE}/api/auth/login`, { username, password });
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
