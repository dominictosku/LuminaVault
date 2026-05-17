import { DocumentAttachment } from './inventory.models';

export type FinanceAccountType =
  | 'Checking' | 'Savings' | 'Cash' | 'CreditCard' | 'Investment' | 'Crypto' | 'Loan' | 'Other';

export const FINANCE_ACCOUNT_TYPES: FinanceAccountType[] = [
  'Checking', 'Savings', 'Cash', 'CreditCard', 'Investment', 'Crypto', 'Loan', 'Other'
];

export type FinanceTransactionKind =
  | 'Income' | 'Expense' | 'Transfer' | 'Buy' | 'Sell' | 'Dividend' | 'Fee';
export const FINANCE_TRANSACTION_KINDS: FinanceTransactionKind[] = [
  'Income', 'Expense', 'Transfer', 'Buy', 'Sell', 'Dividend', 'Fee'
];
export const TRADE_TRANSACTION_KINDS: FinanceTransactionKind[] = ['Buy', 'Sell', 'Dividend', 'Fee'];
export const CASH_TRANSACTION_KINDS: FinanceTransactionKind[] = ['Income', 'Expense', 'Transfer'];

export const INVESTMENT_ACCOUNT_TYPES: FinanceAccountType[] = ['Investment', 'Crypto'];
export const isInvestmentAccount = (type: FinanceAccountType | null | undefined) =>
  type === 'Investment' || type === 'Crypto';

export type FinanceTransactionStatus = 'Pending' | 'Cleared' | 'Reconciled';
export const FINANCE_TRANSACTION_STATUSES: FinanceTransactionStatus[] = ['Pending', 'Cleared', 'Reconciled'];

export type SubscriptionStatus = 'Active' | 'Paused' | 'Cancelled';
export const SUBSCRIPTION_STATUSES: SubscriptionStatus[] = ['Active', 'Paused', 'Cancelled'];

export type SavingsGoalStatus = 'Active' | 'Paused' | 'Achieved' | 'Cancelled';
export const SAVINGS_GOAL_STATUSES: SavingsGoalStatus[] = ['Active', 'Paused', 'Achieved', 'Cancelled'];

export type LoanStatus = 'Active' | 'PaidOff' | 'Closed';
export const LOAN_STATUSES: LoanStatus[] = ['Active', 'PaidOff', 'Closed'];

export interface TaxHoldingRow {
  account: string;
  symbol: string;
  name?: string | null;
  quantity: number;
  averageCost: number;
  costBasis: number;
  priceAtYearEnd?: number | null;
  priceAsOf?: string | null;
  priceIsStale: boolean;
  estimatedValue?: number | null;
  currency: string;
}

export interface TaxAccountBalanceRow {
  account: string;
  currency: string;
  type: string;
  balanceAtYearEnd: number;
}

export interface TaxCategoryTotalRow {
  category: string;
  amount: number;
}

export interface TaxExport {
  year: number;
  asOf: string;
  holdings: TaxHoldingRow[];
  totalSecuritiesValue: number;
  accountBalances: TaxAccountBalanceRow[];
  totalCashBalance: number;
  income: TaxCategoryTotalRow[];
  totalIncome: number;
  expenses: TaxCategoryTotalRow[];
  totalExpenses: number;
  warnings: string[];
}

export interface FinanceAccount {
  id: number;
  name: string;
  institution?: string | null;
  type: FinanceAccountType;
  currency: string;
  startingBalance: number;
  balance: number;
  color: string;
  notes?: string | null;
  isArchived: boolean;
  createdAt: string;
}

export interface FinanceBudget {
  id: number;
  category: string;
  month: string;
  limitAmount: number;
  spent: number;
  remaining: number;
  usedPercent: number;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface FinanceBudgetInput {
  category: string;
  month: string;
  limitAmount: number;
  notes?: string | null;
}

export interface SavingsGoal {
  id: number;
  name: string;
  accountId?: number | null;
  accountName?: string | null;
  currency: string;
  targetAmount: number;
  currentAmount: number;
  remaining: number;
  progressPercent: number;
  targetDate?: string | null;
  status: SavingsGoalStatus;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SavingsGoalInput {
  name: string;
  accountId?: number | null;
  currency: string;
  targetAmount: number;
  currentAmount: number;
  targetDate?: string | null;
  status: SavingsGoalStatus;
  notes?: string | null;
}

export interface Loan {
  id: number;
  name: string;
  lender?: string | null;
  accountId?: number | null;
  accountName?: string | null;
  currency: string;
  principal: number;
  annualInterestRate: number;
  termMonths: number;
  startDate: string;
  extraMonthlyPayment: number;
  monthlyPayment: number;
  currentBalance: number;
  totalInterest: number;
  totalPayments: number;
  paymentsMade: number;
  payoffDate: string;
  nextPaymentDue: number;
  nextPaymentDate?: string | null;
  status: LoanStatus;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface LoanInput {
  name: string;
  lender?: string | null;
  accountId?: number | null;
  currency: string;
  principal: number;
  annualInterestRate: number;
  termMonths: number;
  startDate: string;
  extraMonthlyPayment: number;
  status: LoanStatus;
  notes?: string | null;
}

export interface LoanScheduleRow {
  paymentNumber: number;
  paymentDate: string;
  payment: number;
  principal: number;
  interest: number;
  extraPrincipal: number;
  balance: number;
}

export interface LoanSchedule {
  loanId: number;
  name: string;
  monthlyPayment: number;
  totalPayments: number;
  totalInterest: number;
  totalPrincipal: number;
  paymentsMade: number;
  payoffDate: string;
  currentBalance: number;
  nextPaymentDue: number;
  nextPaymentDate?: string | null;
  rows: LoanScheduleRow[];
}

export interface FinanceBudgetOverview {
  month: string;
  totalBudget: number;
  totalSpent: number;
  remaining: number;
  rows: FinanceBudget[];
}

export interface AccountBalanceSnapshot {
  id: number;
  accountId: number;
  accountName?: string | null;
  currency: string;
  snapshotDate: string;
  actualBalance: number;
  expectedBalance: number;
  difference: number;
  isReconciled: boolean;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AccountBalanceSnapshotInput {
  accountId: number;
  snapshotDate: string;
  actualBalance: number;
  isReconciled: boolean;
  notes?: string | null;
}

export interface FinanceAccountInput {
  name: string;
  institution?: string | null;
  type: FinanceAccountType;
  currency: string;
  startingBalance: number;
  balance: number;
  color: string;
  notes?: string | null;
  isArchived: boolean;
}

export interface TransactionSplit {
  id: number;
  category: string;
  amount: number;
  notes?: string | null;
  sortOrder: number;
}

export interface TransactionSplitInput {
  category: string;
  amount: number;
  notes?: string | null;
}

export const SPLITTABLE_TRANSACTION_KINDS: FinanceTransactionKind[] = ['Income', 'Expense'];
export const supportsSplits = (kind: FinanceTransactionKind | null | undefined) =>
  kind === 'Income' || kind === 'Expense';

export interface FinanceTransaction {
  id: number;
  accountId: number;
  accountName?: string | null;
  transferAccountId?: number | null;
  transferAccountName?: string | null;
  kind: FinanceTransactionKind;
  status: FinanceTransactionStatus;
  occurredOn: string;
  payee: string;
  category: string;
  amount: number;
  description?: string | null;
  notes?: string | null;
  tags: string[];
  symbol?: string | null;
  quantity?: number | null;
  pricePerUnit?: number | null;
  splits: TransactionSplit[];
  createdAt: string;
  updatedAt: string;
}

export interface FinanceTransactionInput {
  accountId: number;
  transferAccountId?: number | null;
  kind: FinanceTransactionKind;
  status: FinanceTransactionStatus;
  occurredOn: string;
  payee: string;
  category: string;
  amount: number;
  description?: string | null;
  notes?: string | null;
  tags: string[];
  symbol?: string | null;
  quantity?: number | null;
  pricePerUnit?: number | null;
  splits?: TransactionSplitInput[] | null;
}

export interface Holding {
  id: number;
  accountId: number;
  accountName?: string | null;
  currency: string;
  symbol: string;
  name?: string | null;
  quantity: number;
  averageCost: number;
  lastPrice?: number | null;
  lastPriceAt?: string | null;
  providerId?: string | null;
  costBasis: number;
  marketValue?: number | null;
  unrealizedPnL?: number | null;
  unrealizedPnLPercent?: number | null;
  realizedPnL: number;
  dividends: number;
  fees: number;
  totalReturn: number;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface HoldingPriceInput {
  lastPrice?: number | null;
  name?: string | null;
  providerId?: string | null;
  notes?: string | null;
}

export interface HoldingAnalyticsTotals {
  marketValue: number;
  costBasis: number;
  unrealizedPnL: number;
  realizedPnL: number;
  dividends: number;
  fees: number;
  totalReturn: number;
  totalReturnPercent?: number | null;
  positionCount: number;
}

export interface HoldingAllocation {
  name: string;
  marketValue: number;
  costBasis: number;
  totalReturn: number;
  percent: number;
}

export interface HoldingPerformer {
  id: number;
  symbol: string;
  name?: string | null;
  accountName: string;
  marketValue: number;
  costBasis: number;
  unrealizedPnL: number;
  totalReturn: number;
  returnPercent?: number | null;
}

export interface HoldingAnalytics {
  generatedAt: string;
  baseCurrency: string;
  totals: HoldingAnalyticsTotals;
  allocationByAccount: HoldingAllocation[];
  allocationBySymbol: HoldingAllocation[];
  topPerformers: HoldingPerformer[];
  worstPerformers: HoldingPerformer[];
}

export interface HoldingRefreshError {
  holdingId: number;
  symbol: string;
  error: string;
}

export interface HoldingRefreshSkipped {
  holdingId: number;
  symbol: string;
  reason: string;
}

export interface HoldingRefreshResult {
  updated: number;
  errors: HoldingRefreshError[];
  skipped: HoldingRefreshSkipped[];
  providers: string[];
}

export interface PriceProviderStatus {
  name: string;
  isConfigured: boolean;
  supportedAccountTypes: string[];
}

export interface MonthlyAccountSummary {
  id: number;
  accountId: number;
  accountName?: string | null;
  currency: string;
  month: string;
  income: number;
  expenses: number;
  net: number;
  openingBalance?: number | null;
  closingBalance?: number | null;
  expectedClosingBalance?: number | null;
  closingDifference?: number | null;
  isReconciled: boolean;
  reconciledAt?: string | null;
  reconciliationNotes?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface MonthlyAccountSummaryInput {
  accountId: number;
  month: string;
  income: number;
  expenses: number;
  openingBalance?: number | null;
  closingBalance?: number | null;
  notes?: string | null;
}

export interface MonthlyReconciliationInput {
  isReconciled: boolean;
  notes?: string | null;
}

export interface Subscription {
  id: number;
  name: string;
  category: string;
  provider?: string | null;
  accountId?: number | null;
  accountName?: string | null;
  amount: number;
  currency: string;
  billingIntervalDays: number;
  startedOn: string;
  nextDueOn: string;
  autoRenew: boolean;
  status: SubscriptionStatus;
  notes?: string | null;
  monthlyAmount: number;
  attachments: DocumentAttachment[];
  createdAt: string;
  updatedAt: string;
}

export interface SubscriptionInput {
  name: string;
  category: string;
  provider?: string | null;
  accountId?: number | null;
  amount: number;
  currency: string;
  billingIntervalDays: number;
  startedOn: string;
  nextDueOn: string;
  autoRenew: boolean;
  status: SubscriptionStatus;
  notes?: string | null;
}

export interface SubscriptionGenerateTransactionInput {
  status: FinanceTransactionStatus;
  advanceNextDueOn: boolean;
}

export interface SubscriptionGenerateTransactionResult {
  transaction: FinanceTransaction;
  subscription: Subscription;
}

export interface SubscriptionGenerateDueResult {
  generatedAt: string;
  throughDate: string;
  created: number;
  skipped: number;
  transactions: FinanceTransaction[];
  subscriptions: Subscription[];
}

export interface FinanceSummary {
  netWorth: number;
  baseCurrency: string;
  fxMissingCurrencies: string[];
  accountNetWorth: number;
  inventoryValue: number;
  holdingsMarketValue: number;
  holdingsCostBasis: number;
  holdingsUnrealizedPnL: number;
  monthlyIncome: number;
  monthlyExpenses: number;
  monthlyCashFlow: number;
  savingsRate: number;
  recurringMonthly: number;
  activeSubscriptionCount: number;
  accountCount: number;
  walletCount: number;
  investmentValue: number;
  recentTransactions: FinanceTransaction[];
  upcomingSubscriptions: {
    id: number;
    name: string;
    category: string;
    nextDueOn: string;
    amount: number;
    currency: string;
    monthlyAmount: number;
  }[];
  monthlySeries: { month: string; income: number; expenses: number; net: number }[];
  categoryBreakdown: { category: string; amount: number }[];
  accountMix: { type: string; balance: number }[];
}

export interface FinanceStatistics {
  generatedAt: string;
  rangeStart: string;
  rangeEnd: string;
  baseCurrency: string;
  fxMissingCurrencies: string[];
  totals: {
    netWorth: number;
    accountNetWorth: number;
    assetValue: number;
    monthlySubscriptionCost: number;
    annualSubscriptionCost: number;
    transactionIncome: number;
    transactionExpenses: number;
    transactionNet: number;
    assetCount: number;
    activeSubscriptionCount: number;
    transactionCount: number;
    summarizedMonthCount: number;
  };
  assetCategoryBreakdown: { category: string; amount: number; count: number; average: number }[];
  subscriptionCategoryBreakdown: { category: string; monthlyAmount: number; annualAmount: number; count: number }[];
  transactionExpenseBreakdown: { category: string; amount: number; count: number; average: number }[];
  transactionIncomeBreakdown: { category: string; amount: number; count: number; average: number }[];
  monthlySeries: { month: string; income: number; expenses: number; net: number; summaryCount: number; transactionCount: number }[];
  accountBalances: { account: string; type: string; balance: number; baseBalance: number; currency: string; color: string }[];
  topExpenses: { id: number; payee: string; category: string; amount: number; occurredOn: string; accountName?: string | null }[];
  subscriptionRunway: { id: number; name: string; category: string; amount: number; currency: string; monthlyAmount: number; monthlyAmountBase: number; annualAmount: number; nextDueOn: string }[];
}

export interface NetWorthSnapshot {
  id: number;
  snapshotDate: string;
  accountNetWorth: number;
  holdingsMarketValue: number;
  holdingsCostBasis: number;
  inventoryValue: number;
  netWorth: number;
  currency: string;
  createdAt: string;
}

export interface ForecastPoint {
  date: string;
  balance: number;
  changeFromYesterday: number;
}

export interface ForecastEvent {
  date: string;
  source: 'Subscription' | 'Pending' | 'Transfer';
  description: string;
  accountId?: number | null;
  accountName?: string | null;
  amount: number;
  currency: string;
  baseAmount: number;
}

export interface CashFlowForecast {
  generatedAt: string;
  from: string;
  to: string;
  days: number;
  baseCurrency: string;
  startingBalance: number;
  endingBalance: number;
  netChange: number;
  lowestBalance: number;
  lowestDate: string;
  eventCount: number;
  daily: ForecastPoint[];
  events: ForecastEvent[];
}
