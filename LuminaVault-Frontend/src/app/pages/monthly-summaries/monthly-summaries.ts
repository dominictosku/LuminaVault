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
  template: `
    <div class="p-6 xl:p-8 fade-in">
      <div class="flex items-center justify-between gap-4 mb-6">
        <div>
          <h1 class="text-2xl font-semibold tracking-tight">Monthly summaries</h1>
          <p class="text-slate-400 text-sm mt-1">Use one income/expense total when your bank only provides monthly sums.</p>
        </div>
        <button class="btn btn-primary" (click)="newSummary()">
          <i class="pi pi-plus"></i> Summary
        </button>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-[1fr_420px] gap-4">
        <section class="space-y-3">
          <div class="surface p-3 grid grid-cols-1 md:grid-cols-[220px_160px_1fr] gap-3">
            <select class="select" [(ngModel)]="accountFilter" (ngModelChange)="fetchSummaries()">
              <option [ngValue]="null">All accounts</option>
              @for (a of accounts(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
            </select>
            <select class="select" [ngModel]="yearFilter()" (ngModelChange)="yearFilter.set($event)">
              <option [ngValue]="null">All years</option>
              @for (year of availableYears(); track year) { <option [ngValue]="year">{{ year }}</option> }
            </select>
            <div class="text-sm text-slate-400 flex items-center">
              Summary months replace detailed transactions for the same account/month in balances and stats.
            </div>
          </div>

          @if (loading()) {
            <div class="space-y-2">
              @for (_ of [1,2,3,4,5]; track _) { <div class="surface h-16 animate-pulse"></div> }
            </div>
          } @else if (summaries().length === 0) {
            <div class="surface p-8 text-center">
              <i class="pi pi-calendar-plus text-4xl text-violet-300/70"></i>
              <div class="mt-3 text-lg">No monthly summaries yet.</div>
              <button class="btn btn-primary mt-4" (click)="newSummary()">
                <i class="pi pi-plus"></i> Add summary
              </button>
            </div>
          } @else if (filteredSummaries().length === 0) {
            <div class="surface p-8 text-center">
              <i class="pi pi-filter text-4xl text-violet-300/70"></i>
              <div class="mt-3 text-lg">No monthly summaries match this year.</div>
              <button class="btn btn-ghost mt-4" (click)="yearFilter.set(null)">
                <i class="pi pi-filter-slash"></i> Clear year
              </button>
            </div>
          } @else {
            <div class="surface overflow-hidden">
              <div class="hidden md:grid grid-cols-[130px_1fr_130px_130px_130px] gap-3 px-4 py-2 text-xs uppercase tracking-wide text-slate-500 border-b border-white/5">
                <div>Month</div><div>Account</div><div class="text-right">Income</div><div class="text-right">Expenses</div><div class="text-right">Net</div>
              </div>
              <div class="divide-y divide-white/5">
                @for (s of filteredSummaries(); track s.id) {
                  <button type="button" class="w-full text-left grid grid-cols-1 md:grid-cols-[130px_1fr_130px_130px_130px] gap-3 px-4 py-3 hover:bg-white/5 transition"
                          (click)="editSummary(s)">
                    <div class="text-sm text-slate-400">{{ s.month | date:'MMM y' }}</div>
                    <div class="min-w-0">
                      <div class="font-medium truncate">{{ s.accountName || 'Account' }}</div>
                      @if (s.notes) { <div class="text-xs text-slate-500 truncate">{{ s.notes }}</div> }
                    </div>
                    <div class="text-right text-emerald-300">{{ s.income | currency:s.currency:'symbol-narrow' }}</div>
                    <div class="text-right text-red-300">{{ s.expenses | currency:s.currency:'symbol-narrow' }}</div>
                    <div class="text-right font-medium" [class.text-emerald-300]="s.net >= 0" [class.text-red-300]="s.net < 0">
                      {{ s.net | currency:s.currency:'symbol-narrow' }}
                    </div>
                  </button>
                }
              </div>
            </div>
          }
        </section>

        <aside class="surface p-5 h-fit">
          <h2 class="font-medium flex items-center gap-2 mb-4">
            <i class="pi pi-pen-to-square text-violet-300"></i>
            {{ editingId() ? 'Edit summary' : 'New summary' }}
          </h2>

          @if (accounts().length === 0) {
            <div class="text-sm text-slate-400 mb-4">Add an account before creating monthly summaries.</div>
          }

          <form (ngSubmit)="save()" class="space-y-4">
            <div>
              <label class="label">Account</label>
              <select class="select" name="account" [(ngModel)]="model.accountId" required>
                <option [ngValue]="0">Choose account</option>
                @for (a of accounts(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
              </select>
            </div>
            <div>
              <label class="label">Month</label>
              <input class="input" type="date" name="month" [(ngModel)]="monthPickerValue" required />
              <div class="text-xs text-slate-500 mt-1">
                Saved as {{ selectedMonthLabel() }}.
              </div>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Income</label>
                <input class="input" type="number" step="0.01" min="0" name="income" [(ngModel)]="model.income" />
              </div>
              <div>
                <label class="label">Expenses</label>
                <input class="input" type="number" step="0.01" min="0" name="expenses" [(ngModel)]="model.expenses" />
              </div>
            </div>
            <div class="surface-muted p-3 flex justify-between text-sm">
              <span class="text-slate-400">Net movement</span>
              <span [class.text-emerald-300]="netPreview() >= 0" [class.text-red-300]="netPreview() < 0">
                {{ netPreview() | currency:currencyPreview():'symbol-narrow' }}
              </span>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Opening balance</label>
                <input class="input" type="number" step="0.01" name="opening" [(ngModel)]="model.openingBalance" />
              </div>
              <div>
                <label class="label">Closing balance</label>
                <input class="input" type="number" step="0.01" name="closing" [(ngModel)]="model.closingBalance" />
              </div>
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
