import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-screen flex">
      <aside class="w-64 shrink-0 border-r border-white/5 bg-black/20 backdrop-blur-xl flex flex-col">
        <div class="px-5 py-5 flex items-center gap-2">
          <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-violet-500 to-pink-500 flex items-center justify-center text-white font-bold">L</div>
          <div>
            <div class="font-semibold tracking-tight">LuminaVault</div>
            <div class="text-[11px] text-slate-400">finance vault</div>
          </div>
        </div>
        <nav class="px-3 py-2 flex-1 space-y-1">
          <a routerLink="/dashboard" routerLinkActive="bg-white/8 text-white" [routerLinkActiveOptions]="{exact:true}"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-chart-line text-violet-300"></i> Overview
          </a>
          <a routerLink="/transactions" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-arrow-right-arrow-left text-violet-300"></i> Transactions
          </a>
          <a routerLink="/monthly-summaries" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-calendar-plus text-violet-300"></i> Monthly sums
          </a>
          <a routerLink="/statistics" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-chart-bar text-violet-300"></i> Statistics
          </a>
          <a routerLink="/accounts" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-wallet text-violet-300"></i> Accounts
          </a>
          <a routerLink="/subscriptions" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-calendar-clock text-violet-300"></i> Subscriptions
          </a>
          <a routerLink="/data" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-file-import text-violet-300"></i> Data
          </a>
          <a routerLink="/settings" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-cog text-violet-300"></i> Settings
          </a>
          <a routerLink="/items" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-box text-violet-300"></i> Assets
          </a>
          <div class="my-3 border-t border-white/5"></div>
          <a routerLink="/rooms" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-home text-violet-300"></i> Rooms
          </a>
          <a routerLink="/planner" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-compass text-violet-300"></i>
            <span>3D Planner</span>
            <span class="ml-auto text-[10px] px-1.5 py-0.5 rounded bg-pink-500/20 text-pink-300 border border-pink-500/30">3D</span>
          </a>
        </nav>
        <div class="px-3 py-3 border-t border-white/5">
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
