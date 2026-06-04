import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { SavingsGoal, SavingsGoalInput } from '../models';

@Injectable({ providedIn: 'root' })
export class GoalsApi {
  private http = inject(HttpClient);

  listGoals(includeInactive = false) {
    return this.http.get<SavingsGoal[]>(`${API_BASE}/api/finance/goals`, {
      params: includeInactive ? new HttpParams().set('includeInactive', true) : undefined,
    });
  }

  createGoal(input: SavingsGoalInput) {
    return this.http.post<SavingsGoal>(`${API_BASE}/api/finance/goals`, input);
  }

  updateGoal(id: number, input: SavingsGoalInput) {
    return this.http.put<SavingsGoal>(`${API_BASE}/api/finance/goals/${id}`, input);
  }

  deleteGoal(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/goals/${id}`);
  }
}
