export interface AssetCategory {
  id: number;
  name: string;
  color: string;
  sortOrder: number;
  createdAt: string;
}

export interface AssetCategoryInput {
  name: string;
  color: string;
  sortOrder: number;
}

export interface FinanceCategory {
  id: number;
  name: string;
  color: string;
  sortOrder: number;
  createdAt: string;
}

export interface FinanceCategoryInput {
  name: string;
  color: string;
  sortOrder: number;
}

export interface FinanceCategoryRule {
  id: number;
  pattern: string;
  category: string;
  matchPayee: boolean;
  matchDescription: boolean;
  isActive: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
}

export interface FinanceCategoryRuleInput {
  pattern: string;
  category: string;
  matchPayee: boolean;
  matchDescription: boolean;
  isActive: boolean;
  priority: number;
}

export interface ExchangeRate {
  id: number;
  currency: string;
  effectiveDate: string;
  rateToBase: number;
  updatedAt: string;
}

export interface ExchangeRateInput {
  currency: string;
  effectiveDate?: string | null;
  rateToBase: number;
}
