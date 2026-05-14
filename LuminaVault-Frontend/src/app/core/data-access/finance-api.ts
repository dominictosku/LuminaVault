import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  FinanceAccount,
  FinanceAccountInput,
  AccountBalanceSnapshot,
  AccountBalanceSnapshotInput,
  FinanceBudget,
  FinanceBudgetInput,
  FinanceBudgetOverview,
  FinanceStatistics,
  FinanceSummary,
  FinanceTransaction,
  FinanceTransactionInput,
  FinanceTransactionKind,
  Holding,
  HoldingPriceInput,
  HoldingRefreshResult,
  PriceProviderStatus,
  MonthlyAccountSummary,
  MonthlyAccountSummaryInput,
  MonthlyReconciliationInput,
  Subscription,
  SubscriptionGenerateTransactionInput,
  SubscriptionGenerateTransactionResult,
  SubscriptionInput,
  DocumentAttachment,
} from '../models';

@Injectable({ providedIn: 'root' })
export class FinanceApi {
  private http = inject(HttpClient);

  financeSummary() {
    return this.http.get<FinanceSummary>(`${API_BASE}/api/finance/summary`);
  }

  financeStatistics() {
    return this.http.get<FinanceStatistics>(`${API_BASE}/api/finance/statistics`);
  }

  listFinanceAccounts(includeArchived = false) {
    return this.http.get<FinanceAccount[]>(`${API_BASE}/api/finance/accounts`, {
      params: includeArchived ? new HttpParams().set('includeArchived', true) : undefined,
    });
  }

  createFinanceAccount(input: FinanceAccountInput) {
    return this.http.post<FinanceAccount>(`${API_BASE}/api/finance/accounts`, input);
  }

  updateFinanceAccount(id: number, input: FinanceAccountInput) {
    return this.http.put<FinanceAccount>(`${API_BASE}/api/finance/accounts/${id}`, input);
  }

  deleteFinanceAccount(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/accounts/${id}`);
  }

  listBudgets(month?: string) {
    const params = month ? new HttpParams().set('month', month) : undefined;
    return this.http.get<FinanceBudget[]>(`${API_BASE}/api/finance/budgets`, { params });
  }

  budgetOverview(month?: string) {
    const params = month ? new HttpParams().set('month', month) : undefined;
    return this.http.get<FinanceBudgetOverview>(`${API_BASE}/api/finance/budgets/overview`, { params });
  }

  createBudget(input: FinanceBudgetInput) {
    return this.http.post<FinanceBudget>(`${API_BASE}/api/finance/budgets`, input);
  }

  updateBudget(id: number, input: FinanceBudgetInput) {
    return this.http.put<FinanceBudget>(`${API_BASE}/api/finance/budgets/${id}`, input);
  }

  deleteBudget(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/budgets/${id}`);
  }

  listHoldings(accountId?: number) {
    const params = accountId ? new HttpParams().set('accountId', accountId) : undefined;
    return this.http.get<Holding[]>(`${API_BASE}/api/finance/holdings`, { params });
  }

  updateHolding(id: number, input: HoldingPriceInput) {
    return this.http.put<Holding>(`${API_BASE}/api/finance/holdings/${id}`, input);
  }

  deleteHolding(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/holdings/${id}`);
  }

  recomputeHoldings() {
    return this.http.post<void>(`${API_BASE}/api/finance/holdings/recompute`, {});
  }

  refreshHoldingPrices(accountId?: number) {
    const params = accountId ? new HttpParams().set('accountId', accountId) : undefined;
    return this.http.post<HoldingRefreshResult>(`${API_BASE}/api/finance/holdings/refresh-prices`, {}, { params });
  }

  listPriceProviders() {
    return this.http.get<PriceProviderStatus[]>(`${API_BASE}/api/finance/price-providers`);
  }

  listBalanceSnapshots(accountId?: number) {
    const params = accountId ? new HttpParams().set('accountId', accountId) : undefined;
    return this.http.get<AccountBalanceSnapshot[]>(`${API_BASE}/api/finance/balance-snapshots`, { params });
  }

  createBalanceSnapshot(input: AccountBalanceSnapshotInput) {
    return this.http.post<AccountBalanceSnapshot>(`${API_BASE}/api/finance/balance-snapshots`, input);
  }

  deleteBalanceSnapshot(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/balance-snapshots/${id}`);
  }

  listFinanceTransactions(opts: {
    q?: string;
    accountId?: number;
    category?: string;
    kind?: FinanceTransactionKind;
    from?: string;
    to?: string;
  } = {}) {
    let params = new HttpParams();
    if (opts.q) params = params.set('q', opts.q);
    if (opts.accountId) params = params.set('accountId', opts.accountId);
    if (opts.category) params = params.set('category', opts.category);
    if (opts.kind) params = params.set('kind', opts.kind);
    if (opts.from) params = params.set('from', opts.from);
    if (opts.to) params = params.set('to', opts.to);
    return this.http.get<FinanceTransaction[]>(`${API_BASE}/api/finance/transactions`, { params });
  }

  createFinanceTransaction(input: FinanceTransactionInput) {
    return this.http.post<FinanceTransaction>(`${API_BASE}/api/finance/transactions`, input);
  }

  updateFinanceTransaction(id: number, input: FinanceTransactionInput) {
    return this.http.put<FinanceTransaction>(`${API_BASE}/api/finance/transactions/${id}`, input);
  }

  deleteFinanceTransaction(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/transactions/${id}`);
  }

  listMonthlySummaries(opts: { accountId?: number; from?: string; to?: string } = {}) {
    let params = new HttpParams();
    if (opts.accountId) params = params.set('accountId', opts.accountId);
    if (opts.from) params = params.set('from', opts.from);
    if (opts.to) params = params.set('to', opts.to);
    return this.http.get<MonthlyAccountSummary[]>(`${API_BASE}/api/finance/monthly-summaries`, {
      params,
    });
  }

  createMonthlySummary(input: MonthlyAccountSummaryInput) {
    return this.http.post<MonthlyAccountSummary>(
      `${API_BASE}/api/finance/monthly-summaries`,
      input,
    );
  }

  updateMonthlySummary(id: number, input: MonthlyAccountSummaryInput) {
    return this.http.put<MonthlyAccountSummary>(
      `${API_BASE}/api/finance/monthly-summaries/${id}`,
      input,
    );
  }

  deleteMonthlySummary(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/monthly-summaries/${id}`);
  }

  reconcileMonthlySummary(id: number, input: MonthlyReconciliationInput) {
    return this.http.post<MonthlyAccountSummary>(
      `${API_BASE}/api/finance/monthly-summaries/${id}/reconcile`,
      input,
    );
  }

  listSubscriptions(includeInactive = false) {
    return this.http.get<Subscription[]>(`${API_BASE}/api/finance/subscriptions`, {
      params: includeInactive ? new HttpParams().set('includeInactive', true) : undefined,
    });
  }

  createSubscription(input: SubscriptionInput) {
    return this.http.post<Subscription>(`${API_BASE}/api/finance/subscriptions`, input);
  }

  updateSubscription(id: number, input: SubscriptionInput) {
    return this.http.put<Subscription>(`${API_BASE}/api/finance/subscriptions/${id}`, input);
  }

  deleteSubscription(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/subscriptions/${id}`);
  }

  generateSubscriptionTransaction(id: number, input: SubscriptionGenerateTransactionInput) {
    return this.http.post<SubscriptionGenerateTransactionResult>(
      `${API_BASE}/api/finance/subscriptions/${id}/generate-transaction`,
      input,
    );
  }

  uploadSubscriptionAttachment(subscriptionId: number, file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<DocumentAttachment>(`${API_BASE}/api/finance/subscriptions/${subscriptionId}/attachments`, fd);
  }

  deleteAttachment(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/attachments/${id}`);
  }

  attachmentUrl(attachment: DocumentAttachment) {
    return `${API_BASE}${attachment.url}`;
  }
}
