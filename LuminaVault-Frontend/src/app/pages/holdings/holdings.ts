import { CurrencyPipe, DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { FinanceApi } from '../../core/data-access/finance-api';
import { aggregateHoldingsByAccount, holdingsTotals } from '../../core/finance-math';
import {
  FinanceAccount,
  Holding,
  HoldingPriceInput,
  HoldingRefreshResult,
  PriceProviderStatus,
} from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';

@Component({
  selector: 'app-holdings',
  imports: [FormsModule, CurrencyPipe, DecimalPipe, DatePipe, NgClass],
  templateUrl: './holdings.html',
})
export class HoldingsComponent {
  private api = inject(FinanceApi);
  private confirmDialog = inject(ConfirmDialogService);

  holdings = signal<Holding[]>([]);
  accounts = signal<FinanceAccount[]>([]);
  providers = signal<PriceProviderStatus[]>([]);
  loading = signal(true);
  saving = signal<number | null>(null);
  refreshing = signal(false);
  error = signal<string | null>(null);
  accountFilter = signal<number | null>(null);
  editingId = signal<number | null>(null);
  lastRefresh = signal<HoldingRefreshResult | null>(null);
  priceInput = '';
  nameInput = '';
  providerIdInput = '';
  notesInput = '';

  filteredHoldings = computed(() => {
    const accountId = this.accountFilter();
    const list = this.holdings();
    return accountId == null ? list : list.filter(h => h.accountId === accountId);
  });

  groups = computed(() => aggregateHoldingsByAccount(this.filteredHoldings()));

  totals = computed(() => holdingsTotals(this.groups()));

  constructor() {
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    forkJoin({
      holdings: this.api.listHoldings(),
      accounts: this.api.listFinanceAccounts(),
      providers: this.api.listPriceProviders(),
    }).subscribe({
      next: r => {
        this.holdings.set(r.holdings);
        this.accounts.set(r.accounts.filter(a =>
          a.type === 'Investment' || a.type === 'Crypto' || r.holdings.some(h => h.accountId === a.id)
        ));
        this.providers.set(r.providers);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startEdit(holding: Holding) {
    this.editingId.set(holding.id);
    this.priceInput = holding.lastPrice != null ? String(holding.lastPrice) : '';
    this.nameInput = holding.name ?? '';
    this.providerIdInput = holding.providerId ?? '';
    this.notesInput = holding.notes ?? '';
    this.error.set(null);
  }

  cancelEdit() {
    this.editingId.set(null);
    this.priceInput = '';
    this.nameInput = '';
    this.providerIdInput = '';
    this.notesInput = '';
  }

  savePrice(holding: Holding) {
    const trimmed = this.priceInput.trim();
    const input: HoldingPriceInput = {
      lastPrice: trimmed === '' ? null : Number(trimmed),
      name: this.nameInput.trim() || null,
      providerId: this.providerIdInput.trim() || null,
      notes: this.notesInput.trim() || null,
    };
    if (input.lastPrice != null && (Number.isNaN(input.lastPrice) || input.lastPrice < 0)) {
      this.error.set('Enter a valid price.');
      return;
    }
    this.saving.set(holding.id);
    this.api.updateHolding(holding.id, input).subscribe({
      next: updated => {
        this.saving.set(null);
        this.holdings.update(list => list.map(h => h.id === updated.id ? updated : h));
        this.cancelEdit();
      },
      error: e => {
        this.saving.set(null);
        this.error.set(e?.error?.error ?? 'Could not update price.');
      },
    });
  }

  refreshPrices() {
    this.refreshing.set(true);
    this.error.set(null);
    const accountId = this.accountFilter() ?? undefined;
    this.api.refreshHoldingPrices(accountId).subscribe({
      next: result => {
        this.lastRefresh.set(result);
        this.refreshing.set(false);
        this.api.listHoldings().subscribe(list => this.holdings.set(list));
      },
      error: e => {
        this.refreshing.set(false);
        this.error.set(e?.error?.error ?? 'Could not refresh prices.');
      },
    });
  }

  dismissRefreshResult() {
    this.lastRefresh.set(null);
  }

  async remove(holding: Holding) {
    const confirmed = await this.confirmDialog.confirm({
      title: `Delete ${holding.symbol}?`,
      message: 'This removes the holding row. If trade transactions still exist, it will be re-created on the next recompute.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteHolding(holding.id).subscribe(() => {
      this.holdings.update(list => list.filter(h => h.id !== holding.id));
      if (this.editingId() === holding.id) this.cancelEdit();
    });
  }

  recompute() {
    this.loading.set(true);
    this.api.recomputeHoldings().subscribe({
      next: () => this.fetch(),
      error: () => this.loading.set(false),
    });
  }

  toneClass(value: number | null | undefined): Record<string, boolean> {
    return {
      'text-emerald-300': value != null && value > 0,
      'text-red-300': value != null && value < 0,
      'text-slate-400': value == null || value === 0,
    };
  }
}
