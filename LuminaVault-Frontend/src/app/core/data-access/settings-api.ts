import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE } from '../api-base';
import {
  AssetCategory,
  AssetCategoryInput,
  FinanceCategory,
  FinanceCategoryInput,
  FinanceCategoryRule,
  FinanceCategoryRuleInput,
  ExchangeRate,
  ExchangeRateInput,
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

  listFinanceCategoryRules() {
    return this.http.get<FinanceCategoryRule[]>(`${API_BASE}/api/settings/finance-category-rules`);
  }

  createFinanceCategoryRule(input: FinanceCategoryRuleInput) {
    return this.http.post<FinanceCategoryRule>(`${API_BASE}/api/settings/finance-category-rules`, input);
  }

  updateFinanceCategoryRule(id: number, input: FinanceCategoryRuleInput) {
    return this.http.put<FinanceCategoryRule>(
      `${API_BASE}/api/settings/finance-category-rules/${id}`,
      input,
    );
  }

  deleteFinanceCategoryRule(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/settings/finance-category-rules/${id}`);
  }

  listExchangeRates() {
    return this.http.get<ExchangeRate[]>(`${API_BASE}/api/settings/exchange-rates`);
  }

  createExchangeRate(input: ExchangeRateInput) {
    return this.http.post<ExchangeRate>(`${API_BASE}/api/settings/exchange-rates`, input);
  }

  updateExchangeRate(id: number, input: ExchangeRateInput) {
    return this.http.put<ExchangeRate>(`${API_BASE}/api/settings/exchange-rates/${id}`, input);
  }

  deleteExchangeRate(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/settings/exchange-rates/${id}`);
  }
}
