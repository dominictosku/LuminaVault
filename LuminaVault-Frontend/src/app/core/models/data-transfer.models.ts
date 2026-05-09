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
