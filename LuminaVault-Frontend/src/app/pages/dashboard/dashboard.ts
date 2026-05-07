import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Api } from '../../core/api';
import { StatsSummary } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `
    <div class="p-8 fade-in">
      <div class="flex items-center justify-between mb-8">
        <div>
          <h1 class="text-3xl font-semibold tracking-tight">Dashboard</h1>
          <p class="text-slate-400 text-sm mt-1">Quick view of what you own.</p>
        </div>
        <a routerLink="/items/new" class="btn btn-primary">
          <i class="pi pi-plus"></i> New item
        </a>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
        <div class="glass rounded-2xl p-5">
          <div class="text-xs text-slate-400 uppercase tracking-wider">Total items</div>
          <div class="text-3xl font-semibold mt-2">{{ stats()?.totalItems ?? '—' }}</div>
        </div>
        <div class="glass rounded-2xl p-5">
          <div class="text-xs text-slate-400 uppercase tracking-wider">Estimated value</div>
          <div class="text-3xl font-semibold mt-2">
            {{ stats() ? (stats()!.totalValue | currency) : '—' }}
          </div>
        </div>
        <div class="glass rounded-2xl p-5">
          <div class="text-xs text-slate-400 uppercase tracking-wider">Rooms tracked</div>
          <div class="text-3xl font-semibold mt-2">{{ stats()?.byRoom?.length ?? '—' }}</div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div class="glass rounded-2xl p-5">
          <h2 class="font-medium mb-4 flex items-center gap-2">
            <i class="pi pi-chart-bar text-violet-300"></i> Items by room
          </h2>
          @if (!stats()) {
            <div class="text-slate-500 text-sm">Loading…</div>
          } @else if (stats()!.byRoom.length === 0) {
            <div class="text-slate-500 text-sm">
              No items linked to a room yet. Try the
              <a routerLink="/planner" class="text-violet-300 hover:underline">3D planner</a> or
              <a routerLink="/items/new" class="text-violet-300 hover:underline">add an item</a>.
            </div>
          } @else {
            <div class="space-y-3">
              @for (r of stats()!.byRoom; track r.roomId) {
                <div>
                  <div class="flex justify-between text-sm mb-1">
                    <span>{{ r.roomName }}</span>
                    <span class="text-slate-400">{{ r.count }}</span>
                  </div>
                  <div class="h-2 rounded-full bg-white/5 overflow-hidden">
                    <div class="h-full bg-gradient-to-r from-violet-500 to-pink-500"
                         [style.width.%]="barWidth(r.count)"></div>
                  </div>
                </div>
              }
            </div>
          }
        </div>

        <div class="glass rounded-2xl p-5">
          <h2 class="font-medium mb-4 flex items-center gap-2">
            <i class="pi pi-clock text-violet-300"></i> Recently added
          </h2>
          @if (!stats()) {
            <div class="text-slate-500 text-sm">Loading…</div>
          } @else if (stats()!.recent.length === 0) {
            <div class="text-slate-500 text-sm">Nothing yet.</div>
          } @else {
            <ul class="divide-y divide-white/5">
              @for (i of stats()!.recent; track i.id) {
                <li>
                  <a [routerLink]="['/items', i.id]" class="flex justify-between py-2.5 hover:text-violet-300">
                    <span>{{ i.name }}</span>
                    <span class="text-xs text-slate-500">{{ i.createdAt | date:'mediumDate' }}</span>
                  </a>
                </li>
              }
            </ul>
          }
        </div>
      </div>
    </div>
  `,
})
export class DashboardComponent {
  private api = inject(Api);
  stats = signal<StatsSummary | null>(null);

  constructor() {
    this.api.stats().subscribe({ next: s => this.stats.set(s) });
  }

  barWidth(count: number) {
    const max = Math.max(...(this.stats()?.byRoom.map(r => r.count) ?? [1]), 1);
    return Math.max(4, (count / max) * 100);
  }
}
