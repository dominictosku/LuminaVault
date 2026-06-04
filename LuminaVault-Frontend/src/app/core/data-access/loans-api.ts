import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { Loan, LoanInput, LoanSchedule } from '../models';

@Injectable({ providedIn: 'root' })
export class LoansApi {
  private http = inject(HttpClient);

  listLoans(includeClosed = false) {
    return this.http.get<Loan[]>(`${API_BASE}/api/finance/loans`, {
      params: includeClosed ? new HttpParams().set('includeClosed', true) : undefined,
    });
  }

  loanSchedule(id: number) {
    return this.http.get<LoanSchedule>(`${API_BASE}/api/finance/loans/${id}/schedule`);
  }

  createLoan(input: LoanInput) {
    return this.http.post<Loan>(`${API_BASE}/api/finance/loans`, input);
  }

  updateLoan(id: number, input: LoanInput) {
    return this.http.put<Loan>(`${API_BASE}/api/finance/loans/${id}`, input);
  }

  deleteLoan(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/loans/${id}`);
  }
}
