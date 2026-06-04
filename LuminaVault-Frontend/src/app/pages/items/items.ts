import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { InventoryApi } from '../../core/data-access/inventory-api';
import { Item } from '../../core/models';
import { ProtectedMediaSrcDirective } from '../../shared/protected-media-src.directive';

@Component({
  selector: 'app-items',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, ProtectedMediaSrcDirective],
  templateUrl: './items.html',
  styleUrl: './items.scss'
})
export class ItemsComponent {
  protected api = inject(InventoryApi);
  items = signal<Item[]>([]);
  loading = signal(true);
  // Base64 cursor for the next page of older items, or null when the server has
  // nothing more for the current query.
  nextCursor = signal<string | null>(null);
  loadingMore = signal(false);
  query = '';
  skel = Array(6);

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

  onQuery(_: string) {
    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.fetch(), 250);
  }
}
