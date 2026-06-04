import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { CashFlowForecast } from '../models';

@Injectable({ providedIn: 'root' })
export class ForecastApi {
  private http = inject(HttpClient);

  cashFlowForecast(opts: { days?: number; accountId?: number } = {}) {
    let params = new HttpParams();
    if (opts.days != null) params = params.set('days', opts.days);
    if (opts.accountId != null) params = params.set('accountId', opts.accountId);
    return this.http.get<CashFlowForecast>(`${API_BASE}/api/finance/forecast`, { params });
  }
}
