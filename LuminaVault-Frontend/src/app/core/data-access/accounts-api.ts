import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { FinanceAccount, FinanceAccountInput } from '../models';

@Injectable({ providedIn: 'root' })
export class AccountsApi {
  private http = inject(HttpClient);

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
}
