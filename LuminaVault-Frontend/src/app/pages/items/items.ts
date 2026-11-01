import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { InventoryApi } from '../../core/data-access/inventory-api';
import { Item } from '../../core/models';
import { ProtectedMediaSrcDirective } from '../../shared/protected-media-src.directive';
import { ToastService } from '../../shared/toast/toast.service';
import { NotificationCenterService } from '../../shared/notifications/notification-center.service';

type ItemFilter = 'all' | 'needsCharging';

@Component({
  selector: 'app-items',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, ProtectedMediaSrcDirective],
  templateUrl: './items.html',
  styleUrl: './items.scss'
})
export class ItemsComponent {
  protected api = inject(InventoryApi);
  private toast = inject(ToastService);
  private center = inject(NotificationCenterService);

  items = signal<Item[]>([]);
  loading = signal(true);
  filter = signal<ItemFilter>('all');
  // Base64 cursor for the next page of older items, or null when the server has
  // nothing more for the current query.
  nextCursor = signal<string | null>(null);
  loadingMore = signal(false);
  query = '';
  skel = Array(6);

  needsChargingCount = computed(() => this.items().filter(i => i.needsBatteryCharge).length);

  filteredItems = computed(() => {
    const f = this.filter();
    const list = this.items();
    if (f === 'needsCharging') return list.filter(i => i.needsBatteryCharge);
    return list;
  });

  private debounce: any = null;

  constructor() { this.fetch(); }

  fetch() {
    this.loading.set(true);
    this.api.listItems({ q: this.query.trim() || undefined }).subscribe({
      next: page => {
        this.items.set(page.items);
        this.nextCursor.set(page.nextCursor);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  loadMore() {
    const cursor = this.nextCursor();
    if (!cursor || this.loadingMore()) return;
    this.loadingMore.set(true);
    this.api.listItems({ q: this.query.trim() || undefined, cursor }).subscribe({
      next: page => {
        this.items.update(existing => [...existing, ...page.items]);
        this.nextCursor.set(page.nextCursor);
        this.loadingMore.set(false);
      },
      error: () => this.loadingMore.set(false),
    });
  }

  setFilter(f: ItemFilter) {
    this.filter.set(f);
  }

  chargeItem(item: Item, event: MouseEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.api.chargeItem(item.id).subscribe({
      next: updated => {
        this.items.update(list => list.map(i => i.id === updated.id ? updated : i));
        this.toast.success(`Battery charged for ${item.name}.`);
        this.center.refresh();
      },
      error: () => this.toast.error('Failed to mark as charged.'),
    });
  }

  onQuery(_: string) {
    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.fetch(), 250);
  }
}
