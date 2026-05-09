import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import { OdsImportResult } from '../models';

@Injectable({ providedIn: 'root' })
export class DataTransferApi {
  private http = inject(HttpClient);

  exportOds() {
    return this.http.get(`${API_BASE}/api/data/export/ods`, {
      observe: 'response',
      responseType: 'blob',
    });
  }

  importOds(file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<OdsImportResult>(`${API_BASE}/api/data/import/ods`, fd);
  }
}
