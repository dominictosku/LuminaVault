import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { InventoryApi } from '../../core/data-access/inventory-api';
import { Item } from '../../core/models';

@Component({
  selector: 'app-items',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './items.html',
  styleUrl: './items.scss'
})
export class ItemsComponent {
  protected api = inject(InventoryApi);
  items = signal<Item[]>([]);
  loading = signal(true);
  query = '';
  skel = Array(6);

  private debounce: any = null;

  constructor() { this.fetch(); }

  fetch() {
    this.loading.set(true);
    this.api.listItems({ q: this.query.trim() || undefined }).subscribe({
      next: r => { this.items.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  onQuery(_: string) {
    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.fetch(), 250);
  }
}
