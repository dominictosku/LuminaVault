import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  getConfiguredServerUrl,
  setConfiguredServerUrl,
  testServerConnection,
  isTauriEnvironment,
} from '../../core/api-base';
import { AuthService } from '../../core/auth.service';
import { AuthApi } from '../../core/data-access/auth-api';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class LoginComponent {
  private auth = inject(AuthService);
  private api = inject(AuthApi);
  private router = inject(Router);

  username = '';
  password = '';
  setupSecret = '';
  totpCode = '';
  recoveryCode = '';
  loading = signal(false);
  error = signal<string | null>(null);
  mode = signal<'login' | 'register'>('login');
  requiresSetupSecret = signal(false);
  // Second step shown only after the backend reports the password was right but 2FA is on.
  step = signal<'credentials' | 'twoFactor'>('credentials');
  useRecovery = signal(false);

  // Server connection configuration state
  showServerConfig = signal(false);
  serverUrlInput = signal(getConfiguredServerUrl());
  serverConnected = signal<boolean | null>(null);
  testingConnection = signal(false);
  testResult = signal<{ success: boolean; message: string } | null>(null);
  readonly isDesktop = isTauriEnvironment();

  readonly displayServerUrl = computed(() => {
    const configured = getConfiguredServerUrl();
    if (configured) return configured;
    if (this.isDesktop) return 'http://localhost:5256';
    if (typeof window !== 'undefined' && window.location?.origin) return window.location.origin;
    return 'Same Origin';
  });

  constructor() {
    this.checkServerStatus();
  }

  checkServerStatus() {
    this.api.authStatus().subscribe({
      next: s => {
        this.serverConnected.set(true);
        this.mode.set(s.hasUser ? 'login' : 'register');
        this.requiresSetupSecret.set(s.requiresSetupSecret);
        if (this.error()?.startsWith('Cannot reach the server')) {
          this.error.set(null);
        }
      },
      error: () => {
        this.serverConnected.set(false);
        this.error.set(`Cannot reach the server at ${this.displayServerUrl()}. Is the backend running?`);
      },
    });
  }

  toggleServerConfig() {
    this.serverUrlInput.set(getConfiguredServerUrl());
    this.testResult.set(null);
    this.showServerConfig.update(v => !v);
  }

  async testConnection() {
    this.testingConnection.set(true);
    this.testResult.set(null);
    const res = await testServerConnection(this.serverUrlInput());
    this.testingConnection.set(false);
    this.testResult.set({ success: res.success, message: res.message });
  }

  saveServerConfig() {
    setConfiguredServerUrl(this.serverUrlInput());
    this.showServerConfig.set(false);
    this.checkServerStatus();
  }

  resetServerConfig() {
    this.serverUrlInput.set('');
    setConfiguredServerUrl('');
    this.testResult.set(null);
    this.showServerConfig.set(false);
    this.checkServerStatus();
  }

  submit() {
    if (this.loading()) return;
    this.error.set(null);
    this.loading.set(true);

    if (this.mode() === 'register') {
      this.auth.register(this.username, this.password, this.setupSecret).subscribe({
        next: () => this.onAuthed(),
        error: e => this.onError(e),
      });
      return;
    }

    const totp = this.step() === 'twoFactor' && !this.useRecovery() ? this.totpCode.trim() : undefined;
    const recovery = this.step() === 'twoFactor' && this.useRecovery() ? this.recoveryCode.trim() : undefined;
    this.auth.login(this.username, this.password, totp, recovery).subscribe({
      next: () => this.onAuthed(),
      error: e => this.onError(e),
    });
  }

  backToCredentials() {
    this.step.set('credentials');
    this.error.set(null);
    this.totpCode = '';
    this.recoveryCode = '';
    this.useRecovery.set(false);
  }

  private onAuthed() {
    this.loading.set(false);
    this.router.navigate(['/dashboard']);
  }

  private onError(e: any) {
    this.loading.set(false);
    // Correct password, but a second factor is needed: switch to (or stay on) the code step.
    if (e.status === 401 && e?.error?.twoFactorRequired) {
      this.step.set('twoFactor');
      this.error.set(e.error.error ?? null);
      return;
    }
    this.error.set(e?.error?.error ?? (e.status === 401 ? 'Invalid credentials.' : 'Something went wrong.'));
  }
}
