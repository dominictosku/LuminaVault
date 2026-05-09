export interface OdsImportResult {
  accounts: number;
  transactions: number;
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
