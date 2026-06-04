import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  Holding,
  HoldingAnalytics,
  HoldingPriceInput,
  HoldingRefreshResult,
  PriceProviderStatus,
} from '../models';

@Injectable({ providedIn: 'root' })
export class HoldingsApi {
  private http = inject(HttpClient);

  listHoldings(accountId?: number) {
    const params = accountId ? new HttpParams().set('accountId', accountId) : undefined;
    return this.http.get<Holding[]>(`${API_BASE}/api/finance/holdings`, { params });
  }

  holdingAnalytics(accountId?: number) {
    const params = accountId ? new HttpParams().set('accountId', accountId) : undefined;
    return this.http.get<HoldingAnalytics>(`${API_BASE}/api/finance/holdings/analytics`, { params });
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
}
