import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Api } from '../../core/api';
import {
  FINANCE_TRANSACTION_KINDS,
  FINANCE_TRANSACTION_STATUSES,
  FinanceAccount,
  FinanceCategory,
  FinanceTransaction,
  FinanceTransactionInput,
  FinanceTransactionKind,
  FinanceTransactionStatus,
} from '../../core/models';

@Component({
  selector: 'app-transactions',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './transactions.html',
  styleUrl: './transactions.scss'
})
export class TransactionsComponent {
  private api = inject(Api);
  accounts = signal<FinanceAccount[]>([]);
  financeCategories = signal<FinanceCategory[]>([]);
  transactions = signal<FinanceTransaction[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);

  query = '';
  accountFilter: number | null = null;
  kindFilter: FinanceTransactionKind | null = null;
  monthFilter = signal('');
  dateValue = new Date().toISOString().substring(0, 10);
  tagsRaw = '';
  private debounce: any = null;

  kinds = FINANCE_TRANSACTION_KINDS;
  statuses = FINANCE_TRANSACTION_STATUSES;
  categories = computed(() => {
    return Array.from(new Set([
      ...this.financeCategories().map(c => c.name),
      ...this.transactions().map(t => t.category).filter(Boolean),
      this.model.category,
    ].filter(Boolean) as string[])).sort();
  });
  filteredTransactions = computed(() => {
    const month = this.monthFilter();
    if (!month) return this.transactions();
    return this.transactions().filter(t => t.occurredOn.substring(0, 7) === month);
  });

  model: FinanceTransactionInput = this.defaultModel();

  constructor() {
    forkJoin({
      accounts: this.api.listFinanceAccounts(),
      transactions: this.api.listFinanceTransactions(),
      financeCategories: this.api.listFinanceCategories(),
    }).subscribe({
      next: r => {
        this.accounts.set(r.accounts);
        this.transactions.set(r.transactions);
        this.financeCategories.set(r.financeCategories);
        this.loading.set(false);
        if (r.accounts[0]) this.model.accountId = r.accounts[0].id;
        if (r.financeCategories[0]) this.model.category = r.financeCategories[0].name;
      },
      error: () => this.loading.set(false),
    });
  }

  fetch() {
    this.loading.set(true);
    this.api.listFinanceTransactions({
      q: this.query.trim() || undefined,
      accountId: this.accountFilter || undefined,
      kind: this.kindFilter || undefined,
      ...this.monthRange(),
    }).subscribe({
      next: tx => { this.transactions.set(tx); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  debouncedFetch() {
    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.fetch(), 250);
  }

  setMonthFilter(value: string) {
    this.monthFilter.set(value);
    this.fetch();
  }

  clearFilters() {
    this.query = '';
    this.accountFilter = null;
    this.kindFilter = null;
    this.monthFilter.set('');
    this.fetch();
  }

  newTransaction() {
    this.reset();
  }

  editTransaction(transaction: FinanceTransaction) {
    this.editingId.set(transaction.id);
    this.error.set(null);
    this.dateValue = transaction.occurredOn.substring(0, 10);
    this.tagsRaw = transaction.tags.join(', ');
    this.model = {
      accountId: transaction.accountId,
      transferAccountId: transaction.transferAccountId ?? null,
      kind: transaction.kind,
      status: transaction.status,
      occurredOn: transaction.occurredOn,
      payee: transaction.payee,
      category: transaction.category,
      amount: transaction.amount,
      description: transaction.description ?? '',
      notes: transaction.notes ?? '',
      tags: transaction.tags,
    };
  }

  save() {
    if (!this.model.accountId) {
      this.error.set('Choose an account.');
      return;
    }
    if (!this.model.payee.trim()) {
      this.error.set('Payee is required.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input: FinanceTransactionInput = {
      ...this.model,
      amount: Number(this.model.amount) || 0,
      transferAccountId: this.model.kind === 'Transfer' ? this.model.transferAccountId : null,
      category: this.model.category || 'General',
      occurredOn: new Date(this.dateValue).toISOString(),
      tags: this.tagsRaw.split(',').map(t => t.trim()).filter(Boolean),
    };
    const op = this.editingId()
      ? this.api.updateFinanceTransaction(this.editingId()!, input)
      : this.api.createFinanceTransaction(input);
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.fetch(); },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  remove() {
    if (!this.editingId()) return;
    if (!confirm('Delete this transaction?')) return;
    this.api.deleteFinanceTransaction(this.editingId()!).subscribe(() => {
      this.reset();
      this.fetch();
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.dateValue = new Date().toISOString().substring(0, 10);
    this.tagsRaw = '';
    this.model = this.defaultModel();
    if (this.accounts()[0]) this.model.accountId = this.accounts()[0].id;
    if (this.financeCategories()[0]) this.model.category = this.financeCategories()[0].name;
  }

  sign(kind: FinanceTransactionKind) {
    return kind === 'Income' ? '+' : kind === 'Expense' ? '-' : '';
  }

  private defaultModel(): FinanceTransactionInput {
    return {
      accountId: 0,
      transferAccountId: null,
      kind: 'Expense' as FinanceTransactionKind,
      status: 'Cleared' as FinanceTransactionStatus,
      occurredOn: new Date().toISOString(),
      payee: '',
      category: 'General',
      amount: 0,
      description: '',
      notes: '',
      tags: [],
    };
  }

  private monthRange() {
    const value = this.monthFilter();
    if (!value) return {};
    const [year, month] = value.split('-').map(Number);
    const lastDay = new Date(year, month, 0).getDate();
    return {
      from: `${value}-01`,
      to: `${value}-${String(lastDay).padStart(2, '0')}`,
    };
  }
}
