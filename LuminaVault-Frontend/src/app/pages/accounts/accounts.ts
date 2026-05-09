import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../../core/api';
import {
  FINANCE_ACCOUNT_TYPES,
  FinanceAccount,
  FinanceAccountInput,
  FinanceAccountType,
} from '../../core/models';

@Component({
  selector: 'app-accounts',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    <div class="p-6 xl:p-8 fade-in">
      <div class="flex items-center justify-between gap-4 mb-6">
        <div>
          <h1 class="text-2xl font-semibold tracking-tight">Accounts & wallets</h1>
          <p class="text-slate-400 text-sm mt-1">{{ accounts().length }} active accounts</p>
        </div>
        <button class="btn btn-primary" (click)="newAccount()">
          <i class="pi pi-plus"></i> Account
        </button>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-[1fr_380px] gap-4">
        <section>
          @if (loading()) {
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
              @for (_ of [1,2,3,4]; track _) { <div class="surface h-32 animate-pulse"></div> }
            </div>
          } @else if (accounts().length === 0) {
            <div class="surface p-8 text-center">
              <i class="pi pi-wallet text-4xl text-teal-300/70"></i>
              <div class="mt-3 text-lg">No accounts yet.</div>
              <button class="btn btn-primary mt-4" (click)="newAccount()">
                <i class="pi pi-plus"></i> Add account
              </button>
            </div>
          } @else {
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
              @for (a of accounts(); track a.id) {
                <button type="button" class="surface p-4 text-left hover:border-teal-300/40 transition"
                        (click)="editAccount(a)">
                  <div class="flex items-start justify-between gap-3">
                    <div class="min-w-0">
                      <div class="flex items-center gap-2">
                        <span class="w-2.5 h-2.5 rounded-sm" [style.background]="a.color"></span>
                        <span class="font-medium truncate">{{ a.name }}</span>
                      </div>
                      <div class="text-xs text-slate-500 mt-1 truncate">
                        {{ a.institution || a.type }} · {{ a.currency }}
                      </div>
                    </div>
                    <span class="text-xs px-2 py-1 rounded bg-slate-800 text-slate-300">{{ a.type }}</span>
                  </div>
                  <div class="mt-5 text-2xl font-semibold">{{ a.balance | currency:a.currency:'symbol-narrow' }}</div>
                  <div class="text-xs text-slate-500 mt-1">Opened {{ a.createdAt | date:'mediumDate' }}</div>
                </button>
              }
            </div>
          }
        </section>

        <aside class="surface p-5 h-fit">
          <h2 class="font-medium flex items-center gap-2 mb-4">
            <i class="pi pi-pen-to-square text-teal-300"></i>
            {{ editingId() ? 'Edit account' : 'New account' }}
          </h2>

          <form (ngSubmit)="save()" class="space-y-4">
            <div>
              <label class="label">Name</label>
              <input class="input" name="name" [(ngModel)]="model.name" required />
            </div>
            <div>
              <label class="label">Institution</label>
              <input class="input" name="institution" [(ngModel)]="model.institution" />
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Type</label>
                <select class="select" name="type" [(ngModel)]="model.type">
                  @for (t of accountTypes; track t) { <option [ngValue]="t">{{ t }}</option> }
                </select>
              </div>
              <div>
                <label class="label">Currency</label>
                <input class="input" name="currency" maxlength="8" [(ngModel)]="model.currency" />
              </div>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Starting balance</label>
                <input class="input" type="number" step="0.01" name="starting" [(ngModel)]="model.startingBalance" />
              </div>
              <div>
                <label class="label">Current balance</label>
                <input class="input" type="number" step="0.01" name="balance" [(ngModel)]="model.balance"
                       [disabled]="editingId() !== null" />
              </div>
            </div>
            <div>
              <label class="label">Color</label>
              <div class="flex gap-2">
                @for (c of colors; track c) {
                  <button type="button" class="w-8 h-8 rounded border border-slate-700"
                          [style.background]="c" [class.ring-2]="model.color === c"
                          [class.ring-teal-300]="model.color === c"
                          (click)="model.color = c">
                  </button>
                }
              </div>
            </div>
            <div>
              <label class="label">Notes</label>
              <textarea class="textarea" rows="3" name="notes" [(ngModel)]="model.notes"></textarea>
            </div>
            <label class="flex items-center gap-2 text-sm text-slate-300">
              <input type="checkbox" name="archived" [(ngModel)]="model.isArchived" />
              Archived
            </label>
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
              <button class="btn btn-primary flex-1 justify-center" type="submit" [disabled]="saving()">
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
export class AccountsComponent {
  private api = inject(Api);
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
