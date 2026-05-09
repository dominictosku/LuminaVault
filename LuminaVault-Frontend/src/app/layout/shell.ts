import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-screen flex">
      <aside class="w-64 shrink-0 border-r border-slate-800 bg-slate-950 flex flex-col">
        <div class="px-5 py-5 flex items-center gap-2">
          <div class="w-9 h-9 rounded-lg bg-teal-400 flex items-center justify-center text-slate-950 font-bold">L</div>
          <div>
            <div class="font-semibold tracking-tight">LuminaVault</div>
            <div class="text-[11px] text-slate-400">finance vault</div>
          </div>
        </div>
        <nav class="px-3 py-2 flex-1 space-y-1">
          <a routerLink="/dashboard" routerLinkActive="bg-slate-800 text-white" [routerLinkActiveOptions]="{exact:true}"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-slate-900 transition">
            <i class="pi pi-chart-line text-teal-300"></i> Overview
          </a>
          <a routerLink="/transactions" routerLinkActive="bg-slate-800 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-slate-900 transition">
            <i class="pi pi-arrow-right-arrow-left text-sky-300"></i> Transactions
          </a>
          <a routerLink="/accounts" routerLinkActive="bg-slate-800 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-slate-900 transition">
            <i class="pi pi-wallet text-emerald-300"></i> Accounts
          </a>
          <a routerLink="/subscriptions" routerLinkActive="bg-slate-800 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-slate-900 transition">
            <i class="pi pi-calendar-clock text-amber-300"></i> Subscriptions
          </a>
          <a routerLink="/items" routerLinkActive="bg-slate-800 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-slate-900 transition">
            <i class="pi pi-box text-indigo-300"></i> Assets
          </a>
        </nav>
        <div class="px-3 py-3 border-t border-slate-800">
          <div class="px-3 py-2 text-xs text-slate-400">
            Signed in as <span class="text-slate-200">{{ auth.username() }}</span>
          </div>
          <button (click)="auth.logout()" class="w-full btn btn-ghost justify-center mt-1">
            <i class="pi pi-sign-out"></i> Sign out
          </button>
        </div>
      </aside>
      <main class="flex-1 min-w-0 relative">
        <div class="hero-glow"></div>
        <div class="relative h-screen overflow-y-auto">
          <router-outlet />
        </div>
      </main>
    </div>
  `,
})
export class ShellComponent {
  protected auth = inject(AuthService);
}
