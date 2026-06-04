import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  FinanceTransaction,
  FinanceTransactionInput,
  FinanceTransactionKind,
  FinanceTransactionPage,
  FinanceTransactionStatus,
} from '../models';

@Injectable({ providedIn: 'root' })
export class TransactionsApi {
  private http = inject(HttpClient);

  listFinanceTransactions(opts: {
    q?: string;
    accountId?: number;
    category?: string;
    kind?: FinanceTransactionKind;
    from?: string;
    to?: string;
    cursor?: string | null;
    pageSize?: number;
  } = {}) {
    let params = new HttpParams();
    if (opts.q) params = params.set('q', opts.q);
    if (opts.accountId) params = params.set('accountId', opts.accountId);
    if (opts.category) params = params.set('category', opts.category);
    if (opts.kind) params = params.set('kind', opts.kind);
    if (opts.from) params = params.set('from', opts.from);
    if (opts.to) params = params.set('to', opts.to);
    if (opts.cursor) params = params.set('cursor', opts.cursor);
    if (opts.pageSize) params = params.set('pageSize', opts.pageSize);
    return this.http.get<FinanceTransactionPage>(`${API_BASE}/api/finance/transactions`, { params });
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

  /// Bulk apply one operation to many transactions in a single request. Server
  /// recalculates balances + holdings once after the batch, not per-row.
  bulkFinanceTransactions(input: {
    ids: number[];
    operation: 'delete' | 'set-category' | 'set-status';
    category?: string | null;
    status?: FinanceTransactionStatus | null;
  }) {
    return this.http.post<{ matched: number; updated: number }>(
      `${API_BASE}/api/finance/transactions/bulk`,
      input,
    );
  }
}
