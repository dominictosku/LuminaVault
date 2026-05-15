export interface OdsImportResult {
  accounts: number;
  transactions: number;
  holdings: number;
  monthlySummaries: number;
  subscriptions: number;
  financeCategories: number;
  assetCategories: number;
  assets: number;
  warnings: string[];
}

export interface OdsPreviewSheet {
  name: string;
  headers: string[];
  sampleRows: string[][];
  suggestedTarget: string;
}

export interface OdsPreviewResult {
  sheets: OdsPreviewSheet[];
}

export interface OdsMappedImportRequest {
  sheetName: string;
  target: string;
  columns: Record<string, string>;
}

export interface BankCsvPreviewResult {
  headers: string[];
  sampleRows: string[][];
  suggestedColumns: Record<string, string>;
}

export interface BankCsvImportRequest {
  accountId: number;
  columns: Record<string, string>;
  defaultCategory: string | null;
  status: string;
}

export interface BankCsvImportResult {
  transactions: number;
  duplicates: number;
  skipped: number;
  warnings: string[];
}
