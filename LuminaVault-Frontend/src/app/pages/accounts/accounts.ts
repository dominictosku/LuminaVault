import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FinanceApi } from '../../core/data-access/finance-api';
import {
  FINANCE_ACCOUNT_TYPES,
  FinanceAccount,
  FinanceAccountInput,
  FinanceAccountType,
} from '../../core/models';

@Component({
  selector: 'app-accounts',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './accounts.html',
  styleUrl: './accounts.scss'
})
export class AccountsComponent {
  private api = inject(FinanceApi);
  accounts = signal<FinanceAccount[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);

  accountTypes = FINANCE_ACCOUNT_TYPES;
  colors = ['#14b8a6', '#38bdf8', '#34d399', '#f59e0b', '#818cf8', '#fb7185'];

  model: FinanceAccountInput = this.defaultModel();

  constructor() {
    this.fetch();
  }

  fetch() {
    this.loading.set(true);
    this.api.listFinanceAccounts().subscribe({
      next: accounts => { this.accounts.set(accounts); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  newAccount() {
    this.reset();
  }

  editAccount(account: FinanceAccount) {
    this.editingId.set(account.id);
    this.error.set(null);
    this.model = {
      name: account.name,
      institution: account.institution ?? '',
      type: account.type,
      currency: account.currency,
      startingBalance: account.startingBalance,
      balance: account.balance,
      color: account.color,
      notes: account.notes ?? '',
      isArchived: account.isArchived,
    };
  }

  save() {
    if (!this.model.name.trim()) {
      this.error.set('Name is required.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input = {
      ...this.model,
      currency: (this.model.currency || 'CHF').toUpperCase(),
      startingBalance: Number(this.model.startingBalance) || 0,
      balance: Number(this.model.balance) || 0,
    };
    const op = this.editingId()
      ? this.api.updateFinanceAccount(this.editingId()!, input)
      : this.api.createFinanceAccount(input);
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.fetch(); },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  remove() {
    if (!this.editingId()) return;
    if (!confirm('Archive or delete this account?')) return;
    this.api.deleteFinanceAccount(this.editingId()!).subscribe(() => {
      this.reset();
      this.fetch();
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.model = this.defaultModel();
  }

  private defaultModel(): FinanceAccountInput {
    return {
      name: '',
      institution: '',
      type: 'Checking' as FinanceAccountType,
      currency: 'CHF',
      startingBalance: 0,
      balance: 0,
      color: '#14b8a6',
      notes: '',
      isArchived: false,
    };
  }
}
