import { CurrencyPipe, DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { FinanceApi } from '../../core/data-access/finance-api';
import { SettingsApi } from '../../core/data-access/settings-api';
import {
  CASH_TRANSACTION_KINDS,
  FINANCE_TRANSACTION_KINDS,
  FINANCE_TRANSACTION_STATUSES,
  FinanceAccount,
  FinanceCategory,
  FinanceTransaction,
  FinanceTransactionInput,
  FinanceTransactionKind,
  FinanceTransactionStatus,
  TRADE_TRANSACTION_KINDS,
  TransactionSplitInput,
  isInvestmentAccount,
  supportsSplits,
} from '../../core/models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { ToastService } from '../../shared/toast/toast.service';
import { tradeAmount } from '../../core/finance-math';
import { parseReceipt, recognizeReceipt } from '../../core/receipt-ocr';
import { FilterPreset } from '../../shared/filters/filter-presets.service';
import { FilterStateController } from '../../shared/filters/filter-state.controller';

type TransactionFilters = {
  q: string;
  account: number | null;
  kind: FinanceTransactionKind | null;
  month: string;
};

const DEFAULT_FILTERS: TransactionFilters = { q: '', account: null, kind: null, month: '' };

@Component({
  selector: 'app-transactions',
  imports: [FormsModule, CurrencyPipe, DatePipe, DecimalPipe, NgClass],
  templateUrl: './transactions.html',
  styleUrl: './transactions.scss',
  providers: [FilterStateController],
})
export class TransactionsComponent {
  private api = inject(FinanceApi);
  private settingsApi = inject(SettingsApi);
  private confirmDialog = inject(ConfirmDialogService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  protected filters = inject<FilterStateController<TransactionFilters>>(FilterStateController);

  accounts = signal<FinanceAccount[]>([]);
  financeCategories = signal<FinanceCategory[]>([]);
  transactions = signal<FinanceTransaction[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  editingId = signal<number | null>(null);
  /// Base64 cursor for the next page of older transactions, or null when the
  /// server has nothing more to send for the current filters.
  nextCursor = signal<string | null>(null);
  loadingMore = signal(false);

  query = '';
  accountFilter: number | null = null;
  kindFilter: FinanceTransactionKind | null = null;
  monthFilter = signal('');
  dateValue = new Date().toISOString().substring(0, 10);
  tagsRaw = '';
  private debounce: any = null;
  presetName = '';

  /// IDs of rows the user has checkboxed for a bulk operation. Reset when filters
  /// change (via fetch()) so a stale selection from a hidden row can't be acted on.
  selectedIds = signal<Set<number>>(new Set());
  bulkCategory = '';
  bulkStatus: FinanceTransactionStatus | '' = '';
  bulkApplying = signal(false);

  /// OCR state for the "Scan receipt" button. `ocrProgress` is the Tesseract.js
  /// recognition fraction (0–1); the model + WASM (~3 MB) loads on first use.
  ocrRunning = signal(false);
  ocrProgress = signal(0);

  kinds = FINANCE_TRANSACTION_KINDS;
  statuses = FINANCE_TRANSACTION_STATUSES;
  categories = computed(() => {
    return Array.from(new Set([
      ...this.financeCategories().map(c => c.name),
      ...this.transactions().map(t => t.category).filter(Boolean),
      this.model.category,
    ].filter(Boolean) as string[])).sort();
  });

  selectedAccount = computed(() =>
    this.accounts().find(a => a.id === this.model.accountId) ?? null
  );

  availableKinds = computed<FinanceTransactionKind[]>(() => {
    const account = this.selectedAccount();
    return isInvestmentAccount(account?.type)
      ? [...CASH_TRANSACTION_KINDS, ...TRADE_TRANSACTION_KINDS]
      : CASH_TRANSACTION_KINDS;
  });

  isTradeKind(kind: FinanceTransactionKind): boolean {
    return TRADE_TRANSACTION_KINDS.includes(kind);
  }

  needsTradeDetails(kind: FinanceTransactionKind): boolean {
    return kind === 'Buy' || kind === 'Sell';
  }

  // Splits live in their own signal so the form can mutate them without re-creating the model.
  splits = signal<TransactionSplitInput[]>([]);
  canSplit = computed(() => supportsSplits(this.model.kind));
  splitsTotal = computed(() =>
    this.splits().reduce((sum, s) => sum + (Number(s.amount) || 0), 0)
  );
  splitsRemaining = computed(() =>
    Number(((Number(this.model.amount) || 0) - this.splitsTotal()).toFixed(2))
  );
  splitsBalanced = computed(() => Math.abs(this.splitsRemaining()) < 0.005);

  filteredTransactions = computed(() => {
    const month = this.monthFilter();
    if (!month) return this.transactions();
    return this.transactions().filter(t => t.occurredOn.substring(0, 7) === month);
  });

  model: FinanceTransactionInput = this.defaultModel();

  constructor() {
    this.filters.configure({
      storageKey: 'transactions',
      defaults: DEFAULT_FILTERS,
      read: () => this.currentFilters(),
      write: values => this.applyFilters(values),
      onChange: () => this.fetch(),
    });

    const params = this.route.snapshot.queryParamMap;
    this.query = params.get('q') ?? '';
    this.accountFilter = params.has('account') ? Number(params.get('account')) : null;
    this.kindFilter = (params.get('kind') as FinanceTransactionKind | null) || null;
    this.monthFilter.set(params.get('month') ?? '');
    forkJoin({
      accounts: this.api.listFinanceAccounts(),
      transactions: this.api.listFinanceTransactions({
        q: this.query.trim() || undefined,
        accountId: this.accountFilter || undefined,
        kind: this.kindFilter || undefined,
        ...this.monthRange(),
      }),
      financeCategories: this.settingsApi.listFinanceCategories(),
    }).subscribe({
      next: r => {
        this.accounts.set(r.accounts);
        this.transactions.set(r.transactions.items);
        this.nextCursor.set(r.transactions.nextCursor);
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
    this.syncFiltersToUrl();
    // Clear bulk selection on refetch — kept selections from a previous filter
    // would let the user act on rows they can no longer see.
    this.clearSelection();
    this.api.listFinanceTransactions({
      q: this.query.trim() || undefined,
      accountId: this.accountFilter || undefined,
      kind: this.kindFilter || undefined,
      ...this.monthRange(),
    }).subscribe({
      next: page => {
        this.transactions.set(page.items);
        this.nextCursor.set(page.nextCursor);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /// Loads the next page using the cursor returned with the previous page. Filter
  /// changes reset `nextCursor` to null via `fetch()`, so this only fires while the
  /// user is scrolling backward through the currently filtered window.
  loadMore() {
    const cursor = this.nextCursor();
    if (!cursor || this.loadingMore()) return;
    this.loadingMore.set(true);
    this.api.listFinanceTransactions({
      q: this.query.trim() || undefined,
      accountId: this.accountFilter || undefined,
      kind: this.kindFilter || undefined,
      ...this.monthRange(),
      cursor,
    }).subscribe({
      next: page => {
        this.transactions.update(existing => [...existing, ...page.items]);
        this.nextCursor.set(page.nextCursor);
        this.loadingMore.set(false);
      },
      error: () => this.loadingMore.set(false),
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

  currentFilters(): TransactionFilters {
    return {
      q: this.query,
      account: this.accountFilter,
      kind: this.kindFilter,
      month: this.monthFilter(),
    };
  }

  private applyFilters(values: TransactionFilters) {
    this.query = values.q;
    this.accountFilter = values.account;
    this.kindFilter = values.kind;
    this.monthFilter.set(values.month);
  }

  // Template-facing wrappers around the shared controller — keep the existing
  // template API while implementations live in FilterStateController.
  filterPresets = this.filters.presets;
  hasActiveFilters() { return this.filters.hasActive(); }
  clearFilters() { this.filters.clearAll(); }
  clearFilter(name: keyof TransactionFilters) { this.filters.clearOne(name); }
  saveFilterPreset() { this.filters.savePreset(this.presetName); this.presetName = ''; }
  applyFilterPreset(preset?: FilterPreset<TransactionFilters>) { this.filters.applyPreset(preset); }

  private syncFiltersToUrl() {
    const f = this.currentFilters();
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        q: f.q || null,
        account: f.account,
        kind: f.kind,
        month: f.month || null,
      },
      queryParamsHandling: 'merge',
    });
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
      symbol: transaction.symbol ?? '',
      quantity: transaction.quantity ?? null,
      pricePerUnit: transaction.pricePerUnit ?? null,
    };
    this.splits.set(transaction.splits.map(s => ({
      category: s.category,
      amount: s.amount,
      notes: s.notes ?? null,
    })));
  }

  addSplit() {
    const defaultCategory = this.financeCategories()[0]?.name ?? 'General';
    // Seed amount with the unallocated remainder so adding rows in order trivially balances.
    const remainder = Math.max(0, this.splitsRemaining());
    this.splits.update(list => [...list, {
      category: defaultCategory,
      amount: Number(remainder.toFixed(2)),
      notes: null,
    }]);
  }

  removeSplit(index: number) {
    this.splits.update(list => list.filter((_, i) => i !== index));
  }

  clearSplits() {
    this.splits.set([]);
  }

  autoBalanceLastSplit() {
    const list = this.splits();
    if (list.length === 0) return;
    const remainder = this.splitsRemaining();
    const lastIndex = list.length - 1;
    const next = [...list];
    next[lastIndex] = {
      ...next[lastIndex],
      amount: Number((Number(next[lastIndex].amount || 0) + remainder).toFixed(2)),
    };
    this.splits.set(next);
  }

  onAccountChange() {
    if (!this.availableKinds().includes(this.model.kind)) {
      this.model.kind = 'Expense';
    }
  }

  onKindChange() {
    if (!this.isTradeKind(this.model.kind)) {
      this.model.symbol = '';
      this.model.quantity = null;
      this.model.pricePerUnit = null;
    }
    if (!supportsSplits(this.model.kind)) {
      this.splits.set([]);
    }
  }

  recomputeAmountFromTrade() {
    if (!this.needsTradeDetails(this.model.kind)) return;
    const computed = tradeAmount(this.model.quantity, this.model.pricePerUnit);
    if (computed > 0) this.model.amount = computed;
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
    const splits = this.canSplit() && this.splits().length > 0 ? this.splits() : [];
    if (splits.length > 0 && !this.splitsBalanced()) {
      this.error.set(`Splits must sum to ${Number(this.model.amount).toFixed(2)} (off by ${this.splitsRemaining().toFixed(2)}).`);
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const isTrade = this.isTradeKind(this.model.kind);
    const input: FinanceTransactionInput = {
      ...this.model,
      amount: Number(this.model.amount) || 0,
      transferAccountId: this.model.kind === 'Transfer' ? this.model.transferAccountId : null,
      category: this.model.category || 'General',
      occurredOn: new Date(this.dateValue).toISOString(),
      tags: this.tagsRaw.split(',').map(t => t.trim()).filter(Boolean),
      symbol: isTrade ? (this.model.symbol?.trim().toUpperCase() || null) : null,
      quantity: isTrade && this.model.quantity != null ? Number(this.model.quantity) || null : null,
      pricePerUnit: isTrade && this.model.pricePerUnit != null ? Number(this.model.pricePerUnit) || null : null,
      splits: splits.length > 0
        ? splits.map(s => ({
            category: s.category,
            amount: Number(s.amount) || 0,
            notes: s.notes?.trim() || null,
          }))
        : null,
    };
    const isUpdate = this.editingId() != null;
    const op = isUpdate
      ? this.api.updateFinanceTransaction(this.editingId()!, input)
      : this.api.createFinanceTransaction(input);
    op.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(isUpdate ? 'Transaction updated.' : 'Transaction added.');
        this.reset();
        this.fetch();
      },
      error: e => { this.saving.set(false); this.error.set(e?.error?.error ?? 'Save failed.'); },
    });
  }

  async remove() {
    if (!this.editingId()) return;
    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete transaction?',
      message: 'This transaction will be removed from your account history.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.api.deleteFinanceTransaction(this.editingId()!).subscribe(() => {
      this.toast.success('Transaction deleted.');
      this.reset();
      this.fetch();
    });
  }

  // ---- Bulk selection + actions ----

  isSelected(id: number) { return this.selectedIds().has(id); }
  selectedCount = computed(() => this.selectedIds().size);

  allVisibleSelected = computed(() => {
    const rows = this.transactions();
    if (rows.length === 0) return false;
    const sel = this.selectedIds();
    return rows.every(r => sel.has(r.id));
  });

  toggleRow(id: number) {
    this.selectedIds.update(set => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  toggleAllVisible() {
    const rows = this.transactions();
    const allSelected = this.allVisibleSelected();
    this.selectedIds.update(set => {
      const next = new Set(set);
      for (const r of rows) {
        if (allSelected) next.delete(r.id); else next.add(r.id);
      }
      return next;
    });
  }

  clearSelection() { this.selectedIds.set(new Set()); }

  async bulkDelete() {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    const confirmed = await this.confirmDialog.confirm({
      title: `Delete ${ids.length} transaction${ids.length === 1 ? '' : 's'}?`,
      message: 'The selected rows will be removed and account balances will recalculate.',
      confirmText: 'Delete',
    });
    if (!confirmed) return;
    this.bulkApplying.set(true);
    this.api.bulkFinanceTransactions({ ids, operation: 'delete' }).subscribe({
      next: r => {
        this.bulkApplying.set(false);
        this.toast.success(`Deleted ${r.updated} transaction${r.updated === 1 ? '' : 's'}.`);
        this.fetch();
      },
      error: e => {
        this.bulkApplying.set(false);
        this.error.set(e?.error?.error ?? 'Bulk delete failed.');
      },
    });
  }

  bulkApplyCategory() {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0 || !this.bulkCategory.trim()) return;
    this.bulkApplying.set(true);
    const category = this.bulkCategory.trim();
    this.api.bulkFinanceTransactions({ ids, operation: 'set-category', category }).subscribe({
      next: r => {
        this.bulkApplying.set(false);
        this.toast.success(`Re-categorized ${r.updated} transaction${r.updated === 1 ? '' : 's'} → ${category}.`);
        this.bulkCategory = '';
        this.fetch();
      },
      error: e => {
        this.bulkApplying.set(false);
        this.error.set(e?.error?.error ?? 'Bulk category change failed.');
      },
    });
  }

  bulkApplyStatus() {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0 || !this.bulkStatus) return;
    this.bulkApplying.set(true);
    const status = this.bulkStatus as FinanceTransactionStatus;
    this.api.bulkFinanceTransactions({ ids, operation: 'set-status', status }).subscribe({
      next: r => {
        this.bulkApplying.set(false);
        this.toast.success(`Marked ${r.updated} transaction${r.updated === 1 ? '' : 's'} ${status}.`);
        this.bulkStatus = '';
        this.fetch();
      },
      error: e => {
        this.bulkApplying.set(false);
        this.error.set(e?.error?.error ?? 'Bulk status change failed.');
      },
    });
  }

  reset() {
    this.editingId.set(null);
    this.error.set(null);
    this.dateValue = new Date().toISOString().substring(0, 10);
    this.tagsRaw = '';
    this.model = this.defaultModel();
    this.splits.set([]);
    if (this.accounts()[0]) this.model.accountId = this.accounts()[0].id;
    if (this.financeCategories()[0]) this.model.category = this.financeCategories()[0].name;
  }

  /// OCR a receipt photo and pre-fill the form with the parser's best guesses.
  /// Runs entirely in the browser via tesseract.js — no upload, no external service.
  /// We only fill fields the user hasn't touched yet, so re-scanning a second receipt
  /// doesn't silently overwrite the amount they just typed.
  async scanReceipt(input: HTMLInputElement) {
    const file = input.files?.[0];
    if (!file) return;
    input.value = '';
    this.ocrRunning.set(true);
    this.ocrProgress.set(0);
    try {
      const text = await recognizeReceipt(file, p => this.ocrProgress.set(p));
      const parsed = parseReceipt(text);
      if (!parsed.payee && parsed.amount == null && !parsed.date) {
        this.toast.warning('Could not read this receipt. Try a clearer photo.');
        return;
      }
      if (parsed.payee && !this.model.payee.trim()) this.model.payee = parsed.payee;
      if (parsed.amount != null && !this.model.amount) this.model.amount = parsed.amount;
      if (parsed.date) this.dateValue = parsed.date;
      const filled = [
        parsed.payee && 'payee',
        parsed.amount != null && 'amount',
        parsed.date && 'date',
      ].filter(Boolean) as string[];
      this.toast.success(`Receipt scanned — filled ${filled.join(', ')}. Please verify.`);
    } catch (e: any) {
      this.toast.error(e?.message ?? 'Could not process this image.');
    } finally {
      this.ocrRunning.set(false);
      this.ocrProgress.set(0);
    }
  }

  sign(kind: FinanceTransactionKind) {
    if (kind === 'Income' || kind === 'Sell' || kind === 'Dividend') return '+';
    if (kind === 'Expense' || kind === 'Buy' || kind === 'Fee') return '-';
    return '';
  }

  amountToneClass(kind: FinanceTransactionKind): Record<string, boolean> {
    return {
      'text-emerald-300': kind === 'Income' || kind === 'Sell' || kind === 'Dividend',
      'text-red-300': kind === 'Expense' || kind === 'Buy' || kind === 'Fee',
    };
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
      symbol: '',
      quantity: null,
      pricePerUnit: null,
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
