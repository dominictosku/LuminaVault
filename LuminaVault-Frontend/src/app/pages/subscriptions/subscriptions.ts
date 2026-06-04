import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AccountsApi } from '../../core/data-access/accounts-api';
import { AttachmentsApi } from '../../core/data-access/attachments-api';
import { SubscriptionsApi } from '../../core/data-access/subscriptions-api';
import { SettingsApi } from '../../core/data-access/settings-api';
import {
  SUBSCRIPTION_STATUSES,
  BILLING_INTERVAL_UNITS,
  SUBSCRIPTION_INTERVAL_PRESETS,
  BillingIntervalUnit,
  DocumentAttachment,
  FinanceAccount,
  FinanceCategory,
  Subscription,
  SubscriptionInput,
  SubscriptionIntervalPreset,
  SubscriptionStatus,
} from '../../core/models';
import { CrudFormController } from '../../shared/crud-form/crud-form.controller';
import { ToastService } from '../../shared/toast/toast.service';
import { activeSubscriptionsMonthlyTotal } from '../../core/finance-math';
import { ProtectedMediaService } from '../../core/protected-media.service';
import { FilterPreset } from '../../shared/filters/filter-presets.service';
import { FilterStateController } from '../../shared/filters/filter-state.controller';
import {
  DEFAULT_SORT,
  SORT_COLUMN_LABELS,
  SubscriptionSortColumn,
  SubscriptionSortDir,
  SubscriptionSortState,
  defaultDirFor,
  parseSort,
  sortSubscriptions,
} from './subscription-sort';

type SubscriptionFilters = {
  q: string;
  status: SubscriptionStatus | null;
  category: string | null;
  account: number | null;
  /// Serialized "{column}-{dir}" token (e.g. "monthly-desc"), kept as a plain string
  /// so URL params and saved presets stay simple. See parseSort/SubscriptionSortState.
  sort: string;
};

const DEFAULT_FILTERS: SubscriptionFilters = {
  q: '',
  status: null,
  category: null,
  account: null,
  sort: 'due-asc',
};

@Component({
  selector: 'app-subscriptions',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './subscriptions.html',
  styleUrl: './subscriptions.scss',
  providers: [FilterStateController, CrudFormController],
})
export class SubscriptionsComponent {
  protected api = inject(SubscriptionsApi);
  private accountsApi = inject(AccountsApi);
  private attachmentsApi = inject(AttachmentsApi);
  private settingsApi = inject(SettingsApi);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  protected filters = inject<FilterStateController<SubscriptionFilters>>(FilterStateController);
  protected crud = inject<CrudFormController<SubscriptionInput, Subscription>>(CrudFormController);
  private toast = inject(ToastService);
  private media = inject(ProtectedMediaService);
  protected editingId = this.crud.editingId;
  protected saving = this.crud.saving;
  protected error = this.crud.error;
  accounts = signal<FinanceAccount[]>([]);
  financeCategories = signal<FinanceCategory[]>([]);
  subscriptions = signal<Subscription[]>([]);
  loading = signal(true);
  generatingTransaction = signal(false);
  generatingDue = signal(false);
  uploadingAttachment = signal(false);
  automationMessage = signal<string | null>(null);
  /// Card grid vs. compact table. Persisted so the choice sticks across visits.
  viewMode = signal<SubscriptionView>(readStoredView());

  statuses = SUBSCRIPTION_STATUSES;
  intervalUnits = BILLING_INTERVAL_UNITS;
  intervalPresets = SUBSCRIPTION_INTERVAL_PRESETS;
  query = signal('');
  statusFilter = signal<SubscriptionStatus | null>(null);
  categoryFilter = signal<string | null>(null);
  accountFilter = signal<number | null>(null);
  sort = signal<SubscriptionSortState>({ ...DEFAULT_SORT });
  /// "{column}-{dir}" view of `sort` for the dropdown <select> and URL/preset state.
  sortToken = computed(() => `${this.sort().column}-${this.sort().dir}`);
  sortLabel = computed(() => `${SORT_COLUMN_LABELS[this.sort().column]} ${this.sort().dir === 'asc' ? '↑' : '↓'}`);
  presetName = '';
  filterPresets = this.filters.presets;
  startedOn = new Date().toISOString().substring(0, 10);
  nextDueOn = new Date().toISOString().substring(0, 10);
  model: SubscriptionInput = this.defaultModel();

  activeCount = computed(() => this.subscriptions().filter(s => s.status === 'Active').length);
  monthlyTotal = computed(() => activeSubscriptionsMonthlyTotal(this.subscriptions()));
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
    const sort = this.sort();
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

    return sortSubscriptions(filtered, sort);
  });

  constructor() {
    this.filters.configure({
      storageKey: 'subscriptions',
      defaults: DEFAULT_FILTERS,
      read: () => this.currentFilters(),
      write: values => this.applyFilters(values),
      onChange: () => this.syncFiltersToUrl(),
    });
    this.crud.configure({
      create: input => this.api.createSubscription(input),
      update: (id, input) => this.api.updateSubscription(id, input),
      delete: id => this.api.deleteSubscription(id),
      toastSubject: 'Subscription',
      onSaved: () => { this.reset(); this.fetchAll(); },
      onRemoved: () => { this.reset(); this.fetchAll(); },
    });
    const params = this.route.snapshot.queryParamMap;
    this.query.set(params.get('q') ?? '');
    this.statusFilter.set((params.get('status') as SubscriptionStatus | null) || null);
    this.categoryFilter.set(params.get('category'));
    this.accountFilter.set(params.has('account') ? Number(params.get('account')) : null);
    this.sort.set(parseSort(params.get('sort')));
    this.fetchAll();
  }

  fetchAll() {
    this.loading.set(true);
    forkJoin({
      accounts: this.accountsApi.listFinanceAccounts(),
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
    this.crud.startEdit(subscription.id);
    this.startedOn = subscription.startedOn.substring(0, 10);
    this.nextDueOn = subscription.nextDueOn.substring(0, 10);
    this.model = {
      name: subscription.name,
      category: subscription.category,
      provider: subscription.provider ?? '',
      accountId: subscription.accountId ?? null,
      amount: subscription.amount,
      currency: subscription.currency,
      billingIntervalUnit: subscription.billingIntervalUnit,
      billingIntervalCount: subscription.billingIntervalCount,
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
      this.crud.error.set('Name is required.');
      return;
    }
    const input: SubscriptionInput = {
      ...this.model,
      amount: Number(this.model.amount) || 0,
      billingIntervalUnit: this.model.billingIntervalUnit,
      billingIntervalCount: Math.max(1, Number(this.model.billingIntervalCount) || 1),
      currency: (this.model.currency || 'CHF').toUpperCase(),
      category: this.model.category || 'Subscriptions',
      startedOn: new Date(this.startedOn).toISOString(),
      nextDueOn: new Date(this.nextDueOn).toISOString(),
    };
    this.crud.save(input);
  }

  remove() {
    this.crud.remove({
      title: 'Delete subscription?',
      message: 'This recurring subscription record will be removed.',
      confirmText: 'Delete',
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
        this.toast.success('Document uploaded.');
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
    this.attachmentsApi.deleteAttachment(id).subscribe(() => {
      this.toast.success('Document removed.');
      this.fetchAll();
    });
  }

  openAttachment(attachment: DocumentAttachment) {
    this.media.open(this.attachmentsApi.attachmentUrl(attachment));
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
        this.toast.success(`Transaction generated (${status}).`);
        this.fetchAll();
      },
      error: e => {
        this.generatingTransaction.set(false);
        this.error.set(e?.error?.error ?? 'Could not generate transaction.');
      },
    });
  }

  generateDueSubscriptions() {
    this.generatingDue.set(true);
    this.error.set(null);
    this.automationMessage.set(null);
    this.api.generateDueSubscriptions(7).subscribe({
      next: result => {
        this.generatingDue.set(false);
        const message = result.created === 0
          ? 'No upcoming subscription forecasts were needed.'
          : `Created ${result.created} forecast transaction${result.created === 1 ? '' : 's'} through ${new Date(result.throughDate).toLocaleDateString()}.`;
        this.automationMessage.set(message);
        this.toast.success(message);
        this.fetchAll();
      },
      error: e => {
        this.generatingDue.set(false);
        this.error.set(e?.error?.error ?? 'Could not generate upcoming forecasts.');
      },
    });
  }

  filterChanged() {
    this.syncFiltersToUrl();
  }

  setViewMode(mode: SubscriptionView) {
    this.viewMode.set(mode);
    try { localStorage.setItem(VIEW_STORAGE_KEY, mode); } catch { /* private mode / disabled storage */ }
  }

  /// Table header click: toggle direction when the column is already active, otherwise
  /// switch to it with a sensible default direction.
  sortByColumn(column: SubscriptionSortColumn) {
    const current = this.sort();
    const dir: SubscriptionSortDir = current.column === column
      ? (current.dir === 'asc' ? 'desc' : 'asc')
      : defaultDirFor(column);
    this.sort.set({ column, dir });
    this.filterChanged();
  }

  setSortToken(token: string) {
    this.sort.set(parseSort(token));
    this.filterChanged();
  }

  isSorted(column: SubscriptionSortColumn) {
    return this.sort().column === column;
  }

  /// Direction arrow for a column header — empty unless that column is the active sort.
  sortArrow(column: SubscriptionSortColumn): string {
    if (this.sort().column !== column) return '';
    return this.sort().dir === 'asc' ? '↑' : '↓';
  }

  // Template-facing wrappers around FilterStateController.
  hasActiveFilters() { return this.filters.hasActive(); }
  clearFilters() { this.filters.clearAll(); }
  clearFilter(name: keyof SubscriptionFilters) { this.filters.clearOne(name); }
  saveFilterPreset() { this.filters.savePreset(this.presetName); this.presetName = ''; }
  applyFilterPreset(preset?: FilterPreset<SubscriptionFilters>) { this.filters.applyPreset(preset); }

  currentFilters(): SubscriptionFilters {
    return {
      q: this.query(),
      status: this.statusFilter(),
      category: this.categoryFilter(),
      account: this.accountFilter(),
      sort: this.sortToken(),
    };
  }

  private applyFilters(values: SubscriptionFilters) {
    this.query.set(values.q);
    this.statusFilter.set(values.status);
    this.categoryFilter.set(values.category);
    this.accountFilter.set(values.account);
    this.sort.set(parseSort(values.sort));
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
        sort: f.sort === 'due-asc' ? null : f.sort,
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

  applyIntervalPreset(preset: SubscriptionIntervalPreset) {
    this.model.billingIntervalUnit = preset.unit;
    this.model.billingIntervalCount = preset.count;
  }

  intervalPresetMatches(preset: SubscriptionIntervalPreset) {
    return this.model.billingIntervalUnit === preset.unit
      && Number(this.model.billingIntervalCount) === preset.count;
  }

  formatIntervalSummary(unit: BillingIntervalUnit, count: number) {
    const n = Math.max(1, Number(count) || 1);
    if (n === 1) return `every ${unit.toLowerCase()}`;
    return `every ${n} ${unit.toLowerCase()}s`;
  }

  private defaultModel(): SubscriptionInput {
    return {
      name: '',
      category: 'Subscriptions',
      provider: '',
      accountId: null,
      amount: 0,
      currency: 'CHF',
      billingIntervalUnit: 'Month',
      billingIntervalCount: 1,
      startedOn: new Date().toISOString(),
      nextDueOn: new Date().toISOString(),
      autoRenew: true,
      status: 'Active' as SubscriptionStatus,
      notes: '',
    };
  }
}

type SubscriptionView = 'cards' | 'table';
const VIEW_STORAGE_KEY = 'lv_subscriptions_view';

function readStoredView(): SubscriptionView {
  try {
    return localStorage.getItem(VIEW_STORAGE_KEY) === 'table' ? 'table' : 'cards';
  } catch {
    return 'cards';
  }
}
