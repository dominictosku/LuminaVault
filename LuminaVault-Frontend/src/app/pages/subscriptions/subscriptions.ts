import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Api } from '../../core/api';
import {
  SUBSCRIPTION_STATUSES,
  FinanceAccount,
  Subscription,
  SubscriptionInput,
  SubscriptionStatus,
} from '../../core/models';

@Component({
  selector: 'app-subscriptions',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    <div class="p-6 xl:p-8 fade-in">
      <div class="flex items-center justify-between gap-4 mb-6">
        <div>
          <h1 class="text-2xl font-semibold tracking-tight">Subscriptions</h1>
          <p class="text-slate-400 text-sm mt-1">{{ activeCount() }} active · {{ monthlyTotal() | currency:'CHF':'symbol-narrow' }}/mo</p>
        </div>
        <button class="btn btn-primary" (click)="newSubscription()">
          <i class="pi pi-plus"></i> Subscription
        </button>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-[1fr_420px] gap-4">
        <section>
          @if (loading()) {
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
              @for (_ of [1,2,3,4,5,6]; track _) { <div class="surface h-36 animate-pulse"></div> }
            </div>
          } @else if (subscriptions().length === 0) {
            <div class="surface p-8 text-center">
              <i class="pi pi-calendar-clock text-4xl text-amber-300/80"></i>
              <div class="mt-3 text-lg">No subscriptions yet.</div>
              <button class="btn btn-primary mt-4" (click)="newSubscription()">
                <i class="pi pi-plus"></i> Add subscription
              </button>
            </div>
          } @else {
            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
              @for (s of subscriptions(); track s.id) {
                <button type="button" class="surface p-4 text-left hover:border-amber-300/40 transition"
                        (click)="editSubscription(s)">
                  <div class="flex items-start justify-between gap-3">
                    <div class="min-w-0">
                      <div class="font-medium truncate">{{ s.name }}</div>
                      <div class="text-xs text-slate-500 mt-1 truncate">
                        {{ s.provider || s.category }} · {{ s.accountName || 'No account' }}
                      </div>
                    </div>
                    <span class="text-xs px-2 py-1 rounded"
                          [class.bg-emerald-400/10]="s.status === 'Active'"
                          [class.text-emerald-300]="s.status === 'Active'"
                          [class.bg-amber-400/10]="s.status === 'Paused'"
                          [class.text-amber-300]="s.status === 'Paused'"
                          [class.bg-slate-700]="s.status === 'Cancelled'"
                          [class.text-slate-300]="s.status === 'Cancelled'">
                      {{ s.status }}
                    </span>
                  </div>
                  <div class="mt-5 flex items-end justify-between gap-3">
                    <div>
                      <div class="text-2xl font-semibold">{{ s.monthlyAmount | currency:s.currency:'symbol-narrow' }}</div>
                      <div class="text-xs text-slate-500">monthly equivalent</div>
                    </div>
                    <div class="text-right">
                      <div class="text-sm">{{ s.amount | currency:s.currency:'symbol-narrow' }}</div>
                      <div class="text-xs text-slate-500">due {{ s.nextDueOn | date:'MMM d' }}</div>
                    </div>
                  </div>
                </button>
              }
            </div>
          }
        </section>

        <aside class="surface p-5 h-fit">
          <h2 class="font-medium flex items-center gap-2 mb-4">
            <i class="pi pi-pen-to-square text-amber-300"></i>
            {{ editingId() ? 'Edit subscription' : 'New subscription' }}
          </h2>

          <form (ngSubmit)="save()" class="space-y-4">
            <div>
              <label class="label">Name</label>
              <input class="input" name="name" [(ngModel)]="model.name" required />
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Category</label>
                <input class="input" name="category" [(ngModel)]="model.category" list="subscription-categories" />
                <datalist id="subscription-categories">
                  @for (c of categories(); track c) { <option [value]="c"></option> }
                </datalist>
              </div>
              <div>
                <label class="label">Provider</label>
                <input class="input" name="provider" [(ngModel)]="model.provider" />
              </div>
            </div>
            <div>
              <label class="label">Account</label>
              <select class="select" name="account" [(ngModel)]="model.accountId">
                <option [ngValue]="null">No account</option>
                @for (a of accounts(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
              </select>
            </div>
            <div class="grid grid-cols-3 gap-3">
              <div>
                <label class="label">Price</label>
                <input class="input" type="number" step="0.01" min="0" name="amount" [(ngModel)]="model.amount" required />
              </div>
              <div>
                <label class="label">Currency</label>
                <input class="input" name="currency" maxlength="8" [(ngModel)]="model.currency" />
              </div>
              <div>
                <label class="label">Interval</label>
                <select class="select" name="interval" [(ngModel)]="model.billingIntervalDays">
                  <option [ngValue]="30">Monthly</option>
                  <option [ngValue]="90">Quarterly</option>
                  <option [ngValue]="182">Half-year</option>
                  <option [ngValue]="365">Yearly</option>
                </select>
              </div>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Started</label>
                <input class="input" type="date" name="started" [(ngModel)]="startedOn" />
              </div>
              <div>
                <label class="label">Next due</label>
                <input class="input" type="date" name="due" [(ngModel)]="nextDueOn" />
              </div>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label">Status</label>
                <select class="select" name="status" [(ngModel)]="model.status">
                  @for (s of statuses; track s) { <option [ngValue]="s">{{ s }}</option> }
                </select>
              </div>
              <label class="flex items-end gap-2 text-sm text-slate-300 pb-2">
                <input type="checkbox" name="autoRenew" [(ngModel)]="model.autoRenew" />
                Auto-renew
              </label>
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
export class SubscriptionsComponent {
  private api = inject(Api);
  accounts = signal<FinanceAccount[]>([]);
  subscriptions = signal<Subscription[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);

  statuses = SUBSCRIPTION_STATUSES;
  startedOn = new Date().toISOString().substring(0, 10);
  nextDueOn = new Date().toISOString().substring(0, 10);
  model: SubscriptionInput = this.defaultModel();

  activeCount = computed(() => this.subscriptions().filter(s => s.status === 'Active').length);
  monthlyTotal = computed(() => this.subscriptions()
    .filter(s => s.status === 'Active')
    .reduce((sum, s) => sum + s.monthlyAmount, 0));
  categories = computed(() => {
    const base = ['Obligatorisch', 'Karriere', 'Hobby', 'Körper', 'Software', 'Insurance', 'Utilities'];
    return Array.from(new Set([...base, ...this.subscriptions().map(s => s.category).filter(Boolean)])).sort();
  });

  constructor() {
    this.fetchAll();
  }

  fetchAll() {
    this.loading.set(true);
    forkJoin({
      accounts: this.api.listFinanceAccounts(),
      subscriptions: this.api.listSubscriptions(true),
    }).subscribe({
      next: r => {
        this.accounts.set(r.accounts);
        this.subscriptions.set(r.subscriptions);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  newSubscription() {
    this.reset();
  }

  editSubscription(subscription: Subscription) {
    this.editingId.set(subscription.id);
    this.error.set(null);
    this.startedOn = subscription.startedOn.substring(0, 10);
    this.nextDueOn = subscription.nextDueOn.substring(0, 10);
    this.model = {
      name: subscription.name,
      category: subscription.category,
      provider: subscription.provider ?? '',
      accountId: subscription.accountId ?? null,
      amount: subscription.amount,
      currency: subscription.currency,
      billingIntervalDays: subscription.billingIntervalDays,
      startedOn: subscription.startedOn,
      nextDueOn: subscription.nextDueOn,
      autoRenew: subscription.autoRenew,
      status: subscription.status,
      notes: subscription.notes ?? '',
    };
  }

  save() {
    if (!this.model.name.trim()) {
      this.error.set('Name is required.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const input: SubscriptionInput = {
      ...this.model,
      amount: Number(this.model.amount) || 0,
      billingIntervalDays: Number(this.model.billingIntervalDays) || 30,
      currency: (this.model.currency || 'CHF').toUpperCase(),
      category: this.model.category || 'Subscriptions',
      startedOn: new Date(this.startedOn).toISOString(),
      nextDueOn: new Date(this.nextDueOn).toISOString(),
    };
    const op = this.editingId()
      ? this.api.updateSubscription(this.editingId()!, input)
      : this.api.createSubscription(input);
    op.subscribe({
      next: () => { this.saving.set(false); this.reset(); this.fetchAll(); },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  remove() {
    if (!this.editingId()) return;
    if (!confirm('Delete this subscription?')) return;
    this.api.deleteSubscription(this.editingId()!).subscribe(() => {
      this.reset();
      this.fetchAll();
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.startedOn = new Date().toISOString().substring(0, 10);
    this.nextDueOn = new Date().toISOString().substring(0, 10);
    this.model = this.defaultModel();
  }

  private defaultModel(): SubscriptionInput {
    return {
      name: '',
      category: 'Subscriptions',
      provider: '',
      accountId: null,
      amount: 0,
      currency: 'CHF',
      billingIntervalDays: 30,
      startedOn: new Date().toISOString(),
      nextDueOn: new Date().toISOString(),
      autoRenew: true,
      status: 'Active' as SubscriptionStatus,
      notes: '',
    };
  }
}
