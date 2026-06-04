import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { TaxExport } from '../models';

@Injectable({ providedIn: 'root' })
export class TaxApi {
  private http = inject(HttpClient);

  taxExport(year: number) {
    return this.http.get<TaxExport>(`${API_BASE}/api/finance/tax-export`, {
      params: new HttpParams().set('year', year),
    });
  }

  taxExportOds(year: number) {
    return this.http.get(`${API_BASE}/api/finance/tax-export/ods`, {
      params: new HttpParams().set('year', year),
      responseType: 'blob',
    });
  }
}
