import { DocumentAttachment } from './inventory.models';

export type FinanceAccountType =
  | 'Checking' | 'Savings' | 'Cash' | 'CreditCard' | 'Investment' | 'Crypto' | 'Loan' | 'Other';

export const FINANCE_ACCOUNT_TYPES: FinanceAccountType[] = [
  'Checking', 'Savings', 'Cash', 'CreditCard', 'Investment', 'Crypto', 'Loan', 'Other'
];

export type FinanceTransactionKind = 'Income' | 'Expense' | 'Transfer';
export const FINANCE_TRANSACTION_KINDS: FinanceTransactionKind[] = ['Income', 'Expense', 'Transfer'];

export type FinanceTransactionStatus = 'Pending' | 'Cleared' | 'Reconciled';
export const FINANCE_TRANSACTION_STATUSES: FinanceTransactionStatus[] = ['Pending', 'Cleared', 'Reconciled'];

export type SubscriptionStatus = 'Active' | 'Paused' | 'Cancelled';
export const SUBSCRIPTION_STATUSES: SubscriptionStatus[] = ['Active', 'Paused', 'Cancelled'];

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

export interface FinanceSummary {
  netWorth: number;
  accountNetWorth: number;
  inventoryValue: number;
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
  accountBalances: { account: string; type: string; balance: number; currency: string; color: string }[];
  topExpenses: { id: number; payee: string; category: string; amount: number; occurredOn: string; accountName?: string | null }[];
  subscriptionRunway: { id: number; name: string; category: string; amount: number; currency: string; monthlyAmount: number; annualAmount: number; nextDueOn: string }[];
}
