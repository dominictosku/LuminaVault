import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { FinanceApi } from '../../core/data-access/finance-api';
import { SettingsApi } from '../../core/data-access/settings-api';
import {
  SUBSCRIPTION_STATUSES,
  FinanceAccount,
  FinanceCategory,
  Subscription,
  SubscriptionInput,
  SubscriptionStatus,
} from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { FilterPreset, FilterPresetsService } from '../../shared/filters/filter-presets.service';

type SubscriptionFilters = {
  q: string;
  status: SubscriptionStatus | null;
  category: string | null;
  account: number | null;
  sort: SubscriptionSort;
};

@Component({
  selector: 'app-subscriptions',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './subscriptions.html',
  styleUrl: './subscriptions.scss'
})
export class SubscriptionsComponent {
  protected api = inject(FinanceApi);
  private settingsApi = inject(SettingsApi);
  private confirmDialog = inject(ConfirmDialogService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private filterPresetsService = inject(FilterPresetsService);
  accounts = signal<FinanceAccount[]>([]);
  financeCategories = signal<FinanceCategory[]>([]);
  subscriptions = signal<Subscription[]>([]);
  loading = signal(true);
  saving = signal(false);
  generatingTransaction = signal(false);
  uploadingAttachment = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);

  statuses = SUBSCRIPTION_STATUSES;
  query = signal('');
  statusFilter = signal<SubscriptionStatus | null>(null);
  categoryFilter = signal<string | null>(null);
  accountFilter = signal<number | null>(null);
  sortBy = signal<SubscriptionSort>('dueAsc');
  presetName = '';
  filterPresets = signal<FilterPreset<SubscriptionFilters>[]>([]);
  startedOn = new Date().toISOString().substring(0, 10);
  nextDueOn = new Date().toISOString().substring(0, 10);
  model: SubscriptionInput = this.defaultModel();

  activeCount = computed(() => this.subscriptions().filter(s => s.status === 'Active').length);
  monthlyTotal = computed(() => this.subscriptions()
    .filter(s => s.status === 'Active')
    .reduce((sum, s) => sum + s.monthlyAmount, 0));
  categories = computed(() => {
    return Array.from(new Set([
      ...this.financeCategories().map(c => c.name),
      ...this.subscriptions().map(s => s.category).filter(Boolean),
      this.model.category,
    ].filter(Boolean) as string[])).sort();
  });

  filteredSubscriptions = computed(() => {
    const q = this.query().trim().toLowerCase();
    const statusFilter = this.statusFilter();
    const categoryFilter = this.categoryFilter();
    const accountFilter = this.accountFilter();
    const sortBy = this.sortBy();
    const filtered = this.subscriptions().filter(s => {
      const matchesQuery = !q ||
        s.name.toLowerCase().includes(q) ||
        (s.provider ?? '').toLowerCase().includes(q) ||
        s.category.toLowerCase().includes(q) ||
        (s.accountName ?? '').toLowerCase().includes(q) ||
        (s.notes ?? '').toLowerCase().includes(q);
      const matchesStatus = !statusFilter || s.status === statusFilter;
      const matchesCategory = !categoryFilter || s.category === categoryFilter;
      const matchesAccount = accountFilter == null ||
        (accountFilter === 0 ? s.accountId == null : s.accountId === accountFilter);
      return matchesQuery && matchesStatus && matchesCategory && matchesAccount;
    });

    return [...filtered].sort((a, b) => {
      switch (sortBy) {
        case 'dueDesc':
          return dateMs(b.nextDueOn) - dateMs(a.nextDueOn);
        case 'monthlyDesc':
          return b.monthlyAmount - a.monthlyAmount;
        case 'monthlyAsc':
          return a.monthlyAmount - b.monthlyAmount;
        case 'nameAsc':
          return a.name.localeCompare(b.name);
        case 'categoryAsc':
          return a.category.localeCompare(b.category) || a.name.localeCompare(b.name);
        case 'statusAsc':
          return statusRank(a.status) - statusRank(b.status) || dateMs(a.nextDueOn) - dateMs(b.nextDueOn);
        case 'dueAsc':
        default:
          return dateMs(a.nextDueOn) - dateMs(b.nextDueOn);
      }
    });
  });

  constructor() {
    this.filterPresets.set(this.filterPresetsService.load<SubscriptionFilters>('subscriptions'));
    const params = this.route.snapshot.queryParamMap;
    this.query.set(params.get('q') ?? '');
    this.statusFilter.set((params.get('status') as SubscriptionStatus | null) || null);
    this.categoryFilter.set(params.get('category'));
    this.accountFilter.set(params.has('account') ? Number(params.get('account')) : null);
    this.sortBy.set((params.get('sort') as SubscriptionSort | null) ?? 'dueAsc');
    this.fetchAll();
  }

  fetchAll() {
    this.loading.set(true);
    forkJoin({
      accounts: this.api.listFinanceAccounts(),
      subscriptions: this.api.listSubscriptions(true),
      financeCategories: this.settingsApi.listFinanceCategories(),
    }).subscribe({
      next: r => {
        this.accounts.set(r.accounts);
        this.subscriptions.set(r.subscriptions);
        this.financeCategories.set(r.financeCategories);
        const preferred = this.preferredCategory();
        if (preferred && this.model.category === 'Subscriptions')
          this.model.category = preferred;
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

  selectedSubscription = computed(() =>
    this.subscriptions().find(s => s.id === this.editingId()) ?? null);

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

  async remove() {
    if (!this.editingId()) return;
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete subscription?',
      message: 'This recurring subscription record will be removed.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteSubscription(this.editingId()!).subscribe(() => {
      this.reset();
      this.fetchAll();
    });
  }

  onUploadAttachment(input: HTMLInputElement) {
    const file = input.files?.[0];
    if (!file || !this.editingId()) return;
    this.uploadingAttachment.set(true);
    this.api.uploadSubscriptionAttachment(this.editingId()!, file).subscribe({
      next: () => {
        this.uploadingAttachment.set(false);
        input.value = '';
        this.fetchAll();
      },
      error: e => {
        this.uploadingAttachment.set(false);
        input.value = '';
        this.error.set(e?.error?.error ?? 'Upload failed.');
      },
    });
  }

  removeAttachment(id: number) {
    this.api.deleteAttachment(id).subscribe(() => this.fetchAll());
  }

  generateTransaction(status: 'Pending' | 'Cleared') {
    if (!this.editingId()) return;
    this.generatingTransaction.set(true);
    this.error.set(null);
    this.api.generateSubscriptionTransaction(this.editingId()!, {
      status,
      advanceNextDueOn: status === 'Cleared',
    }).subscribe({
      next: () => {
        this.generatingTransaction.set(false);
        this.fetchAll();
      },
      error: e => {
        this.generatingTransaction.set(false);
        this.error.set(e?.error?.error ?? 'Could not generate transaction.');
      },
    });
  }

  clearFilters() {
    this.query.set('');
    this.statusFilter.set(null);
    this.categoryFilter.set(null);
    this.accountFilter.set(null);
    this.sortBy.set('dueAsc');
    this.syncFiltersToUrl();
  }

  filterChanged() {
    this.syncFiltersToUrl();
  }

  saveFilterPreset() {
    const name = this.presetName.trim();
    if (!name) return;
    this.filterPresets.set(this.filterPresetsService.save('subscriptions', {
      name,
      values: this.currentFilters(),
    }));
    this.presetName = '';
  }

  applyFilterPreset(preset?: FilterPreset<SubscriptionFilters>) {
    if (!preset) return;
    this.query.set(preset.values.q);
    this.statusFilter.set(preset.values.status);
    this.categoryFilter.set(preset.values.category);
    this.accountFilter.set(preset.values.account);
    this.sortBy.set(preset.values.sort);
    this.syncFiltersToUrl();
  }

  hasActiveFilters() {
    const f = this.currentFilters();
    return !!f.q || !!f.status || !!f.category || f.account != null || f.sort !== 'dueAsc';
  }

  clearFilter(name: keyof SubscriptionFilters) {
    if (name === 'q') this.query.set('');
    if (name === 'status') this.statusFilter.set(null);
    if (name === 'category') this.categoryFilter.set(null);
    if (name === 'account') this.accountFilter.set(null);
    if (name === 'sort') this.sortBy.set('dueAsc');
    this.syncFiltersToUrl();
  }

  currentFilters(): SubscriptionFilters {
    return {
      q: this.query(),
      status: this.statusFilter(),
      category: this.categoryFilter(),
      account: this.accountFilter(),
      sort: this.sortBy(),
    };
  }

  private syncFiltersToUrl() {
    const f = this.currentFilters();
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        q: f.q || null,
        status: f.status,
        category: f.category,
        account: f.account,
        sort: f.sort === 'dueAsc' ? null : f.sort,
      },
      queryParamsHandling: 'merge',
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.startedOn = new Date().toISOString().substring(0, 10);
    this.nextDueOn = new Date().toISOString().substring(0, 10);
    this.model = this.defaultModel();
    this.model.category = this.preferredCategory() ?? this.model.category;
  }

  preferredCategory() {
    return this.financeCategories().find(c => c.name === 'Subscriptions')?.name
      ?? this.financeCategories()[0]?.name;
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

type SubscriptionSort =
  | 'dueAsc'
  | 'dueDesc'
  | 'monthlyDesc'
  | 'monthlyAsc'
  | 'nameAsc'
  | 'categoryAsc'
  | 'statusAsc';

function dateMs(value: string) {
  return new Date(value).getTime();
}

function statusRank(status: SubscriptionStatus) {
  return status === 'Active' ? 0 : status === 'Paused' ? 1 : 2;
}
