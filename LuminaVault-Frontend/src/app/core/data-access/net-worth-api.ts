import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { NetWorthSnapshot } from '../models';

@Injectable({ providedIn: 'root' })
export class NetWorthApi {
  private http = inject(HttpClient);

  netWorthHistory(opts: { from?: string; to?: string } = {}) {
    let params = new HttpParams();
    if (opts.from) params = params.set('from', opts.from);
    if (opts.to) params = params.set('to', opts.to);
    return this.http.get<NetWorthSnapshot[]>(`${API_BASE}/api/finance/net-worth/history`, { params });
  }

  captureNetWorthSnapshot() {
    return this.http.post<NetWorthSnapshot>(`${API_BASE}/api/finance/net-worth/snapshot`, {});
  }
}
