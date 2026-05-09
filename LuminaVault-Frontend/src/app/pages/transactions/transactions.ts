import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Api } from '../../core/api';
import {
  FINANCE_TRANSACTION_KINDS,
  FINANCE_TRANSACTION_STATUSES,
  FinanceAccount,
  FinanceTransaction,
  FinanceTransactionInput,
  FinanceTransactionKind,
  FinanceTransactionStatus,
} from '../../core/models';

@Component({
  selector: 'app-transactions',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    <div class="p-6 xl:p-8 fade-in">
      <div class="flex items-center justify-between gap-4 mb-6">
        <div>
          <h1 class="text-2xl font-semibold tracking-tight">Transactions</h1>
          <p class="text-slate-400 text-sm mt-1">{{ transactions().length }} entries</p>
        </div>
        <button class="btn btn-primary" (click)="newTransaction()">
          <i class="pi pi-plus"></i> Transaction
        </button>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-[1fr_420px] gap-4">
        <section class="space-y-3">
          <div class="surface p-3 grid grid-cols-1 md:grid-cols-[1fr_180px_150px] gap-3">
            <div class="relative">
              <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-slate-500"></i>
              <input class="input pl-9" placeholder="Search payee, category, notes"
                     [(ngModel)]="query" (ngModelChange)="debouncedFetch()" />
            </div>
            <select class="select" [(ngModel)]="accountFilter" (ngModelChange)="fetch()">
              <option [ngValue]="null">All accounts</option>
              @for (a of accounts(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
            </select>
            <select class="select" [(ngModel)]="kindFilter" (ngModelChange)="fetch()">
              <option [ngValue]="null">All types</option>
              @for (k of kinds; track k) { <option [ngValue]="k">{{ k }}</option> }
            </select>
          </div>

          @if (loading()) {
            <div class="space-y-2">
              @for (_ of [1,2,3,4,5,6]; track _) { <div class="surface h-16 animate-pulse"></div> }
            </div>
          } @else if (transactions().length === 0) {
            <div class="surface p-8 text-center">
              <i class="pi pi-receipt text-4xl text-sky-300/70"></i>
              <div class="mt-3 text-lg">No transactions yet.</div>
              <button class="btn btn-primary mt-4" (click)="newTransaction()">
                <i class="pi pi-plus"></i> Add transaction
              </button>
            </div>
          } @else {
            <div class="surface overflow-hidden">
              <div class="hidden md:grid grid-cols-[110px_1fr_150px_130px] gap-3 px-4 py-2 text-xs uppercase tracking-wide text-slate-500 border-b border-slate-800">
                <div>Date</div><div>Details</div><div>Account</div><div class="text-right">Amount</div>
              </div>
              <div class="divide-y divide-slate-800">
                @for (t of transactions(); track t.id) {
                  <button type="button" class="w-full text-left grid grid-cols-1 md:grid-cols-[110px_1fr_150px_130px] gap-3 px-4 py-3 hover:bg-slate-900/70 transition"
                          (click)="editTransaction(t)">
                    <div class="text-sm text-slate-400">{{ t.occurredOn | date:'MMM d, y' }}</div>
                    <div class="min-w-0">
                      <div class="font-medium truncate">{{ t.payee }}</div>
                      <div class="text-xs text-slate-500 truncate">
                        {{ t.category }}
                        @if (t.kind === 'Transfer') { · {{ t.transferAccountName || 'Transfer' }} }
                      </div>
                    </div>
                    <div class="text-sm text-slate-400 truncate">{{ t.accountName || 'Account' }}</div>
                    <div class="text-right font-medium"
                         [class.text-emerald-300]="t.kind === 'Income'"
                         [class.text-red-300]="t.kind === 'Expense'">
                      {{ sign(t.kind) }}{{ t.amount | currency:'CHF':'symbol-narrow' }}
                    </div>
                  </button>
                }
              </div>
            </div>
          }
        </section>

        <aside class="surface p-5 h-fit">
          <h2 class="font-medium flex items-center gap-2 mb-4">
            <i class="pi pi-pen-to-square text-sky-300"></i>
            {{ editingId() ? 'Edit transaction' : 'New transaction' }}
          </h2>

          @if (accounts().length === 0) {
            <div class="text-sm text-slate-400 mb-4">
              Add an account or wallet before recording transactions.
            </div>
          }

          <form (ngSubmit)="save()" class="space-y-4">
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Type</label>
                <select class="select" name="kind" [(ngModel)]="model.kind">
                  @for (k of kinds; track k) { <option [ngValue]="k">{{ k }}</option> }
                </select>
              </div>
              <div>
                <label class="label">Status</label>
                <select class="select" name="status" [(ngModel)]="model.status">
                  @for (s of statuses; track s) { <option [ngValue]="s">{{ s }}</option> }
                </select>
              </div>
            </div>
            <div>
              <label class="label">Account</label>
              <select class="select" name="account" [(ngModel)]="model.accountId" required>
                <option [ngValue]="0">Choose account</option>
                @for (a of accounts(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
              </select>
            </div>
            @if (model.kind === 'Transfer') {
              <div>
                <label class="label">Destination</label>
                <select class="select" name="transfer" [(ngModel)]="model.transferAccountId">
                  <option [ngValue]="null">Choose destination</option>
                  @for (a of accounts(); track a.id) {
                    @if (a.id !== model.accountId) { <option [ngValue]="a.id">{{ a.name }}</option> }
                  }
                </select>
              </div>
            }
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Date</label>
                <input class="input" type="date" name="date" [(ngModel)]="dateValue" required />
              </div>
              <div>
                <label class="label">Amount</label>
                <input class="input" type="number" step="0.01" min="0" name="amount" [(ngModel)]="model.amount" required />
              </div>
            </div>
            <div>
              <label class="label">Payee</label>
              <input class="input" name="payee" [(ngModel)]="model.payee" required />
            </div>
            <div>
              <label class="label">Category</label>
              <input class="input" name="category" [(ngModel)]="model.category" list="finance-categories" />
              <datalist id="finance-categories">
                @for (c of categories(); track c) { <option [value]="c"></option> }
              </datalist>
            </div>
            <div>
              <label class="label">Tags</label>
              <input class="input" name="tags" [(ngModel)]="tagsRaw" placeholder="comma separated" />
            </div>
            <div>
              <label class="label">Notes</label>
              <textarea class="textarea" rows="3" name="notes" [(ngModel)]="model.notes"></textarea>
            </div>
            @if (error()) {
              <div class="text-red-300 text-sm bg-red-500/10 border border-red-500/30 rounded-lg px-3 py-2">
                {{ error() }}
              </div>
            }
            <div class="flex gap-2">
              @if (editingId()) {
                <button type="button" class="btn btn-danger" (click)="remove()" [disabled]="saving()">
                  <i class="pi pi-trash"></i>
                </button>
              }
              <button type="button" class="btn btn-ghost flex-1 justify-center" (click)="reset()">Cancel</button>
              <button class="btn btn-primary flex-1 justify-center" type="submit" [disabled]="saving() || accounts().length === 0">
                @if (saving()) { <i class="pi pi-spin pi-spinner"></i> } @else { <i class="pi pi-check"></i> }
                Save
              </button>
            </div>
          </form>
        </aside>
      </div>
    </div>
  `,
})
export class TransactionsComponent {
  private api = inject(Api);
  accounts = signal<FinanceAccount[]>([]);
  transactions = signal<FinanceTransaction[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);

  query = '';
  accountFilter: number | null = null;
  kindFilter: FinanceTransactionKind | null = null;
  dateValue = new Date().toISOString().substring(0, 10);
  tagsRaw = '';
  private debounce: any = null;

  kinds = FINANCE_TRANSACTION_KINDS;
  statuses = FINANCE_TRANSACTION_STATUSES;
  categories = computed(() => {
    const base = ['Salary', 'Food', 'Housing', 'Transport', 'Health', 'Career', 'Hobby', 'Savings', 'Investments', 'Subscriptions'];
    return Array.from(new Set([...base, ...this.transactions().map(t => t.category).filter(Boolean)])).sort();
  });

  model: FinanceTransactionInput = this.defaultModel();

  constructor() {
    forkJoin({
      accounts: this.api.listFinanceAccounts(),
      transactions: this.api.listFinanceTransactions(),
    }).subscribe({
      next: r => {
        this.accounts.set(r.accounts);
        this.transactions.set(r.transactions);
        this.loading.set(false);
        if (r.accounts[0]) this.model.accountId = r.accounts[0].id;
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
    }).subscribe({
      next: tx => { this.transactions.set(tx); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  debouncedFetch() {
    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.fetch(), 250);
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
}
