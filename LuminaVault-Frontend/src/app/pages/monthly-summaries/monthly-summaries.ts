import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Api } from '../../core/api';
import {
  FinanceAccount,
  MonthlyAccountSummary,
  MonthlyAccountSummaryInput,
} from '../../core/models';

@Component({
  selector: 'app-monthly-summaries',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './monthly-summaries.html',
  styleUrl: './monthly-summaries.scss'
})
export class MonthlySummariesComponent {
  private api = inject(Api);
  accounts = signal<FinanceAccount[]>([]);
  summaries = signal<MonthlyAccountSummary[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);
  accountFilter: number | null = null;
  yearFilter = signal<number | null>(null);
  monthPickerValue = new Date().toISOString().substring(0, 10);
  model: MonthlyAccountSummaryInput = this.defaultModel();

  netPreview = computed(() => (Number(this.model.income) || 0) - (Number(this.model.expenses) || 0));
  availableYears = computed(() => {
    return Array.from(new Set(
      this.summaries().map(s => new Date(s.month).getFullYear()).filter(year => !Number.isNaN(year))
    )).sort((a, b) => b - a);
  });
  filteredSummaries = computed(() => {
    const year = this.yearFilter();
    if (!year) return this.summaries();
    return this.summaries().filter(s => new Date(s.month).getFullYear() === year);
  });

  constructor() {
    forkJoin({
      accounts: this.api.listFinanceAccounts(),
      summaries: this.api.listMonthlySummaries(),
    }).subscribe({
      next: r => {
        this.accounts.set(r.accounts);
        this.summaries.set(r.summaries);
        if (r.accounts[0]) this.model.accountId = r.accounts[0].id;
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  fetchSummaries() {
    this.loading.set(true);
    this.api.listMonthlySummaries({ accountId: this.accountFilter || undefined }).subscribe({
      next: summaries => { this.summaries.set(summaries); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  newSummary() {
    this.reset();
  }

  editSummary(summary: MonthlyAccountSummary) {
    this.editingId.set(summary.id);
    this.error.set(null);
    this.monthPickerValue = summary.month.substring(0, 10);
    this.model = {
      accountId: summary.accountId,
      month: summary.month,
      income: summary.income,
      expenses: summary.expenses,
      openingBalance: summary.openingBalance ?? null,
      closingBalance: summary.closingBalance ?? null,
      notes: summary.notes ?? '',
    };
  }

  save() {
    if (!this.model.accountId) {
      this.error.set('Choose an account.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input: MonthlyAccountSummaryInput = {
      ...this.model,
      month: this.monthStartIso(),
      income: Number(this.model.income) || 0,
      expenses: Number(this.model.expenses) || 0,
      openingBalance: this.model.openingBalance == null ? null : Number(this.model.openingBalance),
      closingBalance: this.model.closingBalance == null ? null : Number(this.model.closingBalance),
    };
    const op = this.editingId()
      ? this.api.updateMonthlySummary(this.editingId()!, input)
      : this.api.createMonthlySummary(input);
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.fetchSummaries(); },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  remove() {
    if (!this.editingId()) return;
    if (!confirm('Delete this monthly summary?')) return;
    this.api.deleteMonthlySummary(this.editingId()!).subscribe(() => {
      this.reset();
      this.fetchSummaries();
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.monthPickerValue = new Date().toISOString().substring(0, 10);
    this.model = this.defaultModel();
    if (this.accounts()[0]) this.model.accountId = this.accounts()[0].id;
  }

  currencyPreview() {
    return this.accounts().find(a => a.id === this.model.accountId)?.currency ?? 'CHF';
  }

  selectedMonthLabel() {
    return new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric' })
      .format(new Date(this.monthStartIso()));
  }

  private monthStartIso() {
    const [year, month] = this.monthPickerValue.split('-').map(Number);
    return new Date(Date.UTC(year, month - 1, 1)).toISOString();
  }

  private defaultModel(): MonthlyAccountSummaryInput {
    return {
      accountId: 0,
      month: new Date().toISOString(),
      income: 0,
      expenses: 0,
      openingBalance: null,
      closingBalance: null,
      notes: '',
    };
  }
}
