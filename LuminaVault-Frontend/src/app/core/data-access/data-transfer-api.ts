import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  BankCsvImportRequest,
  BankCsvImportResult,
  BankCsvPreviewResult,
  OdsImportResult,
  OdsMappedImportRequest,
  OdsPreviewResult,
} from '../models';

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

  previewOds(file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<OdsPreviewResult>(`${API_BASE}/api/data/import/ods/preview`, fd);
  }

  importMappedOds(file: File, mapping: OdsMappedImportRequest) {
    const fd = new FormData();
    fd.append('file', file);
    fd.append('mappingJson', JSON.stringify(mapping));
    return this.http.post<OdsImportResult>(`${API_BASE}/api/data/import/ods/mapped`, fd);
  }

  previewBankCsv(file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<BankCsvPreviewResult>(`${API_BASE}/api/data/import/bank-csv/preview`, fd);
  }

  importBankCsv(file: File, mapping: BankCsvImportRequest) {
    const fd = new FormData();
    fd.append('file', file);
    fd.append('mappingJson', JSON.stringify(mapping));
    return this.http.post<BankCsvImportResult>(`${API_BASE}/api/data/import/bank-csv`, fd);
  }
}
