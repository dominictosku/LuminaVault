import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { AccountBalanceSnapshot, AccountBalanceSnapshotInput } from '../models';

@Injectable({ providedIn: 'root' })
export class BalanceSnapshotsApi {
  private http = inject(HttpClient);

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
}
