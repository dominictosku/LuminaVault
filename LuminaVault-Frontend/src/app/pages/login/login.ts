import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { Api } from '../../core/api';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <div class="min-h-screen grid place-items-center p-6 relative overflow-hidden">
      <div class="hero-glow"></div>

      <div class="relative w-full max-w-md fade-in">
        <div class="text-center mb-8">
          <div class="mx-auto w-14 h-14 rounded-lg bg-teal-400 flex items-center justify-center text-slate-950 text-2xl font-bold shadow-lg shadow-teal-950/30">L</div>
          <h1 class="mt-4 text-3xl font-semibold tracking-tight">LuminaVault</h1>
          <p class="text-slate-400 text-sm mt-1">
            {{ mode() === 'login' ? 'Welcome back.' : 'Set up your finance vault.' }}
          </p>
        </div>

        <div class="surface p-6">
          <form (ngSubmit)="submit()">
            <div class="space-y-4">
              <div>
                <label class="label">Username</label>
                <input class="input" name="username" autocomplete="username"
                       [(ngModel)]="username" required minlength="2" />
              </div>
              <div>
                <label class="label">Password</label>
                <input class="input" type="password" name="password"
                       [autocomplete]="mode() === 'login' ? 'current-password' : 'new-password'"
                       [(ngModel)]="password" required minlength="6" />
              </div>

              @if (error()) {
                <div class="text-red-300 text-sm bg-red-500/10 border border-red-500/30 rounded-lg px-3 py-2">
                  {{ error() }}
                </div>
              }

              <button class="btn btn-primary w-full justify-center" type="submit" [disabled]="loading()">
                @if (loading()) {
                  <i class="pi pi-spin pi-spinner"></i>
                } @else {
                  <i class="pi pi-arrow-right"></i>
                }
                {{ mode() === 'login' ? 'Sign in' : 'Create vault' }}
              </button>
            </div>
          </form>
        </div>

        <div class="text-center text-xs text-slate-500 mt-6">
          Single-user app · local SQLite storage
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private auth = inject(AuthService);
  private api = inject(Api);
  private router = inject(Router);

  username = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);
  mode = signal<'login' | 'register'>('login');

  constructor() {
    this.api.authStatus().subscribe({
      next: s => this.mode.set(s.hasUser ? 'login' : 'register'),
      error: () => this.error.set('Cannot reach the server. Is the backend running?'),
    });
  }

  submit() {
    if (this.loading()) return;
    this.error.set(null);
    this.loading.set(true);
    const op = this.mode() === 'login'
      ? this.auth.login(this.username, this.password)
      : this.auth.register(this.username, this.password);
    op.subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: e => {
        this.loading.set(false);
        this.error.set(e?.error?.error ?? (e.status === 401 ? 'Invalid credentials.' : 'Something went wrong.'));
      },
    });
  }
}
