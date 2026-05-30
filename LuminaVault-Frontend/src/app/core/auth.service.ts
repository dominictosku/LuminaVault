import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AuthApi } from './data-access/auth-api';

const TOKEN_KEY = 'lv_token';
const USER_KEY = 'lv_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private api = inject(AuthApi);
  private router = inject(Router);

  private _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private _username = signal<string | null>(localStorage.getItem(USER_KEY));

  readonly token = this._token.asReadonly();
  readonly username = this._username.asReadonly();
  readonly isAuthenticated = computed(() => !!this._token());

  login(username: string, password: string, totpCode?: string, recoveryCode?: string) {
    return this.api.login(username, password, totpCode, recoveryCode)
      .pipe(tap(r => this.persist(r.token, r.username)));
  }
  register(username: string, password: string, setupSecret?: string) {
    return this.api.register(username, password, setupSecret).pipe(tap(r => this.persist(r.token, r.username)));
  }
  changePassword(currentPassword: string, newPassword: string) {
    return this.api.changePassword(currentPassword, newPassword).pipe(tap(r => this.persist(r.token, r.username)));
  }
  logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._token.set(null);
    this._username.set(null);
    this.router.navigate(['/login']);
  }

  private persist(token: string, username: string) {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, username);
    this._token.set(token);
    this._username.set(username);
  }
}
