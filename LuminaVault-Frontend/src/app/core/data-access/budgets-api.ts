import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { FinanceBudget, FinanceBudgetInput, FinanceBudgetOverview } from '../models';

@Injectable({ providedIn: 'root' })
export class BudgetsApi {
  private http = inject(HttpClient);

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
}
