import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  FinanceAccount,
  FinanceAccountInput,
  FinanceStatistics,
  FinanceSummary,
  FinanceTransaction,
  FinanceTransactionInput,
  FinanceTransactionKind,
  MonthlyAccountSummary,
  MonthlyAccountSummaryInput,
  Subscription,
  SubscriptionInput,
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
}
