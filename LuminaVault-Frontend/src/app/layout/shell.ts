import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Event as RouterEvent, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '../core/auth.service';
import { NotificationCenterService } from '../shared/notifications/notification-center.service';
import { ToastOutletComponent } from '../shared/toast/toast.component';

type NavItem = { path: string; icon: string; label: string; badge?: string };
type NavGroup = { id: string; label: string; icon: string; items: NavItem[] };

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
          <a routerLink="/notifications" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-bell text-violet-300"></i>
            <span class="flex-1">Notifications</span>
            @if (notifications.unread() > 0) {
              <span class="text-[10px] px-1.5 py-0.5 rounded bg-pink-500/20 text-pink-300 border border-pink-500/30">{{ notifications.unread() }}</span>
            }
          </a>

          @for (group of groups; track group.id) {
            <div class="pt-1">
              <button type="button" (click)="toggleGroup(group.id)"
                      [attr.aria-expanded]="isExpanded(group.id)"
                      [class.text-white]="isGroupActive(group)"
                      class="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
                <i class="pi {{ group.icon }} text-violet-300"></i>
                <span class="flex-1 text-left text-sm font-medium">{{ group.label }}</span>
                <i class="pi pi-chevron-right text-[10px] text-slate-400 transition-transform duration-200"
                   [class.rotate-90]="isExpanded(group.id)"></i>
              </button>
              @if (isExpanded(group.id)) {
                <div class="mt-1 ml-4 pl-3 border-l border-white/5 space-y-1">
                  @for (item of group.items; track item.path) {
                    <a [routerLink]="item.path" routerLinkActive="bg-white/8 text-white"
                       class="flex items-center gap-3 px-3 py-2 rounded-lg text-slate-300 hover:bg-white/5 transition">
                      <i class="pi {{ item.icon }} text-violet-300"></i>
                      <span class="flex-1">{{ item.label }}</span>
                      @if (item.badge) {
                        <span class="text-[10px] px-1.5 py-0.5 rounded bg-pink-500/20 text-pink-300 border border-pink-500/30">{{ item.badge }}</span>
                      }
                    </a>
                  }
                </div>
              }
            </div>
          }

          <div class="my-3 border-t border-white/5"></div>
          <a routerLink="/data" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-file-import text-violet-300"></i> Data
          </a>
          <a routerLink="/settings" routerLinkActive="bg-white/8 text-white"
             class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-slate-300 hover:bg-white/5 transition">
            <i class="pi pi-cog text-violet-300"></i> Settings
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
  protected notifications = inject(NotificationCenterService);
  private router = inject(Router);

  // Closed by default — desktop CSS forces it visible via lg:translate-x-0.
  protected sidebarOpen = signal(false);

  protected readonly groups: NavGroup[] = [
    {
      id: 'money', label: 'Money', icon: 'pi-money-bill', items: [
        { path: '/transactions', icon: 'pi-arrow-right-arrow-left', label: 'Transactions' },
        { path: '/monthly-summaries', icon: 'pi-calendar-plus', label: 'Monthly sums' },
        { path: '/statistics', icon: 'pi-chart-bar', label: 'Statistics' },
        { path: '/subscriptions', icon: 'pi-calendar-clock', label: 'Subscriptions' },
      ],
    },
    {
      id: 'planning', label: 'Planning', icon: 'pi-bullseye', items: [
        { path: '/budgets', icon: 'pi-chart-pie', label: 'Budgets' },
        { path: '/goals', icon: 'pi-flag', label: 'Goals' },
        { path: '/forecast', icon: 'pi-chart-line', label: 'Forecast' },
      ],
    },
    {
      id: 'assets', label: 'Assets', icon: 'pi-briefcase', items: [
        { path: '/accounts', icon: 'pi-wallet', label: 'Accounts' },
        { path: '/holdings', icon: 'pi-chart-line', label: 'Holdings' },
        { path: '/items', icon: 'pi-box', label: 'Items' },
      ],
    },
    {
      id: 'home', label: 'Home', icon: 'pi-home', items: [
        { path: '/rooms', icon: 'pi-th-large', label: 'Rooms' },
        { path: '/planner', icon: 'pi-compass', label: '3D Planner', badge: '3D' },
      ],
    },
  ];

  // Groups start collapsed; the group containing the active route auto-expands.
  protected expandedGroups = signal<ReadonlySet<string>>(new Set());

  constructor() {
    this.router.events
      .pipe(filter((e: RouterEvent) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => {
        this.sidebarOpen.set(false);
        this.autoExpandActiveGroup();
        // Repull the badge on every navigation. Cheap (one int) and keeps it fresh
        // without polling. The Notifications page also pushes refreshes after writes.
        this.notifications.refresh();
      });
    this.autoExpandActiveGroup();
    this.notifications.refresh();
  }

  toggleSidebar() { this.sidebarOpen.update(v => !v); }
  closeSidebar() { this.sidebarOpen.set(false); }

  toggleGroup(id: string) {
    this.expandedGroups.update(s => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  isExpanded(id: string): boolean { return this.expandedGroups().has(id); }

  isGroupActive(group: NavGroup): boolean {
    const url = this.router.url;
    return group.items.some(i => url === i.path || url.startsWith(i.path + '/'));
  }

  private autoExpandActiveGroup() {
    const active = this.groups.find(g => this.isGroupActive(g));
    if (!active) return;
    this.expandedGroups.update(s => {
      if (s.has(active.id)) return s;
      const next = new Set(s);
      next.add(active.id);
      return next;
    });
  }
}
