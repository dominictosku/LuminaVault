import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Event as RouterEvent, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '../core/auth.service';
import { ToastOutletComponent } from '../shared/toast/toast.component';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastOutletComponent],
  template: `
    <div class="min-h-screen flex flex-col lg:flex-row">
      <!-- Mobile top bar: hamburger + branding. Hidden on lg+ where the sidebar is always visible. -->
      <header class="lg:hidden sticky top-0 z-40 flex items-center gap-3 px-4 h-14 border-b border-white/5 bg-black/40 backdrop-blur-xl">
        <button type="button" (click)="toggleSidebar()" aria-label="Open menu"
                class="w-10 h-10 rounded-lg hover:bg-white/5 flex items-center justify-center text-slate-200">
          <i class="pi pi-bars text-lg"></i>
        </button>
        <div class="flex items-center gap-2">
          <div class="w-7 h-7 rounded-lg bg-gradient-to-br from-violet-500 to-pink-500 flex items-center justify-center text-white text-sm font-bold">L</div>
          <div class="font-semibold tracking-tight">LuminaVault</div>
        </div>
      </header>

      <!-- Click-catcher backdrop only rendered when the drawer is open on mobile. -->
      @if (sidebarOpen()) {
        <button type="button" aria-label="Close menu" (click)="closeSidebar()"
                class="lg:hidden fixed inset-0 z-40 bg-black/60 backdrop-blur-sm cursor-default"></button>
      }

      <aside
        [class.translate-x-0]="sidebarOpen()"
        [class.-translate-x-full]="!sidebarOpen()"
        class="fixed lg:sticky inset-y-0 left-0 top-0 z-50 w-64 shrink-0 border-r border-white/5
               bg-black/40 lg:bg-black/20 backdrop-blur-xl flex flex-col
               transform transition-transform duration-200 ease-out
               lg:translate-x-0 lg:h-screen">
        <div class="px-5 py-5 flex items-center gap-2">
          <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-violet-500 to-pink-500 flex items-center justify-center text-white font-bold">L</div>
          <div>
            <div class="font-semibold tracking-tight">LuminaVault</div>
            <div class="text-[11px] text-slate-400">finance vault</div>
          </div>
          <button type="button" (click)="closeSidebar()" aria-label="Close menu"
                  class="lg:hidden ml-auto w-8 h-8 rounded-lg hover:bg-white/5 flex items-center justify-center text-slate-300">
            <i class="pi pi-times text-sm"></i>
          </button>
        </div>
        <nav class="px-3 py-2 flex-1 space-y-1 overflow-y-auto">
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
          <a routerLink="/budgets" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-chart-pie text-violet-300"></i> Budgets
          </a>
          <a routerLink="/accounts" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-wallet text-violet-300"></i> Accounts
          </a>
          <a routerLink="/holdings" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-chart-line text-violet-300"></i> Holdings
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
        <!-- On mobile the top bar is sticky and ~3.5rem tall; subtract it from the scroll viewport. -->
        <div class="relative h-[calc(100vh-3.5rem)] lg:h-screen overflow-y-auto">
          <router-outlet />
        </div>
      </main>
    </div>
    <app-toast-outlet />
  `,
})
export class ShellComponent {
  protected auth = inject(AuthService);
  private router = inject(Router);

  // Closed by default — desktop CSS forces it visible via lg:translate-x-0.
  protected sidebarOpen = signal(false);

  constructor() {
    // Auto-close the drawer when navigating, so tapping a link on mobile
    // doesn't leave the menu open over the new page.
    this.router.events
      .pipe(filter((e: RouterEvent) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => this.sidebarOpen.set(false));
  }

  toggleSidebar() { this.sidebarOpen.update(v => !v); }
  closeSidebar() { this.sidebarOpen.set(false); }
}
