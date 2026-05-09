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
