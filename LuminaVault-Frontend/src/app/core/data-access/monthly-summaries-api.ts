import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  MonthlyAccountSummary,
  MonthlyAccountSummaryInput,
  MonthlyReconciliationInput,
} from '../models';

@Injectable({ providedIn: 'root' })
export class MonthlySummariesApi {
  private http = inject(HttpClient);

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
}
