import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { Api } from '../../core/api';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss'
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
