import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  AssetCategory,
  AssetCategoryInput,
  FinanceCategory,
  FinanceCategoryInput,
} from '../models';

@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private http = inject(HttpClient);

  listAssetCategories() {
    return this.http.get<AssetCategory[]>(`${API_BASE}/api/settings/asset-categories`);
  }

  createAssetCategory(input: AssetCategoryInput) {
    return this.http.post<AssetCategory>(`${API_BASE}/api/settings/asset-categories`, input);
  }

  updateAssetCategory(id: number, input: AssetCategoryInput) {
    return this.http.put<AssetCategory>(`${API_BASE}/api/settings/asset-categories/${id}`, input);
  }

  deleteAssetCategory(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/settings/asset-categories/${id}`);
  }

  listFinanceCategories() {
    return this.http.get<FinanceCategory[]>(`${API_BASE}/api/settings/finance-categories`);
  }

  createFinanceCategory(input: FinanceCategoryInput) {
    return this.http.post<FinanceCategory>(`${API_BASE}/api/settings/finance-categories`, input);
  }

  updateFinanceCategory(id: number, input: FinanceCategoryInput) {
    return this.http.put<FinanceCategory>(
      `${API_BASE}/api/settings/finance-categories/${id}`,
      input,
    );
  }

  deleteFinanceCategory(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/settings/finance-categories/${id}`);
  }
}
