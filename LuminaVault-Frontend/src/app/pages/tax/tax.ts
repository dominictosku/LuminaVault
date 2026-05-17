import { CurrencyPipe, DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FinanceApi } from '../../core/data-access/finance-api';
import { TaxExport } from '../../core/models';

@Component({
  selector: 'app-tax',
  imports: [FormsModule, CurrencyPipe, DecimalPipe, DatePipe, NgClass],
  templateUrl: './tax.html',
})
export class TaxComponent {
  private api = inject(FinanceApi);

  year = signal(new Date().getFullYear() - 1);
  bundle = signal<TaxExport | null>(null);
  loading = signal(false);
  downloading = signal(false);
  error = signal<string | null>(null);

  yearOptions = computed(() => {
    const current = new Date().getFullYear();
    return Array.from({ length: 7 }, (_, i) => current - i);
  });

  totalWealth = computed(() => {
    const b = this.bundle();
    return b ? b.totalSecuritiesValue + b.totalCashBalance : 0;
  });

  baseCurrency = computed(() => {
    const b = this.bundle();
    return b?.accountBalances[0]?.currency ?? 'CHF';
  });

  constructor() {
    this.load();
  }

  setYear(year: number) {
    this.year.set(year);
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.api.taxExport(this.year()).subscribe({
      next: b => { this.bundle.set(b); this.loading.set(false); },
      error: e => { this.loading.set(false); this.error.set(e?.error?.error ?? 'Failed to load tax bundle.'); },
    });
  }

  downloadOds() {
    this.downloading.set(true);
    this.api.taxExportOds(this.year()).subscribe({
      next: blob => {
        this.downloading.set(false);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `LuminaVault-tax-${this.year()}.ods`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: e => { this.downloading.set(false); this.error.set(e?.error?.error ?? 'Download failed.'); },
    });
  }

  staleClass(stale: boolean) {
    return { 'text-amber-200': stale, 'text-slate-300': !stale };
  }
}
