import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { FinanceStatistics, FinanceSummary } from '../models';

@Injectable({ providedIn: 'root' })
export class FinanceSummaryApi {
  private http = inject(HttpClient);

  financeSummary() {
    return this.http.get<FinanceSummary>(`${API_BASE}/api/finance/summary`);
  }

  financeStatistics() {
    return this.http.get<FinanceStatistics>(`${API_BASE}/api/finance/statistics`);
  }
}
