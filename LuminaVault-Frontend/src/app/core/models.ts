export interface AuthResponse { token: string; username: string; }
export interface AuthStatus { hasUser: boolean; }

export interface House { id: number; name: string; description?: string; }

export interface Room {
  id: number; houseId: number; name: string; color: string;
  x: number; z: number; width: number; depth: number; height: number;
}

export type FurnitureKind =
  | 'Cabinet' | 'Drawer' | 'Shelf' | 'Wardrobe' | 'Desk'
  | 'Table' | 'Sofa' | 'Bed' | 'Box' | 'Other';

export const FURNITURE_KINDS: FurnitureKind[] = [
  'Cabinet','Drawer','Shelf','Wardrobe','Desk','Table','Sofa','Bed','Box','Other'
];

export interface Furniture {
  id: number; roomId: number; name: string; kind: FurnitureKind;
  x: number; y: number; z: number;
  width: number; depth: number; height: number;
  rotationY: number;
  itemCount: number; containerCount: number;
}

export interface Container {
  id: number; furnitureId: number; name: string;
  description?: string; itemCount: number;
}

export interface ItemPhoto { id: number; url: string; contentType: string; }

export interface Item {
  id: number;
  name: string;
  description?: string;
  brand?: string;
  model?: string;
  serialNumber?: string;
  value?: number;
  purchaseDate?: string;
  warrantyUntil?: string;
  quantity: number;
  notes?: string;
  tags: string[];
  furnitureId?: number;
  furnitureName?: string;
  containerId?: number;
  containerName?: string;
  roomId?: number;
  roomName?: string;
  createdAt: string;
  updatedAt: string;
  photos: ItemPhoto[];
  modelUrl?: string | null;
}

export interface ItemInput {
  name: string;
  description?: string | null;
  brand?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  value?: number | null;
  purchaseDate?: string | null;
  warrantyUntil?: string | null;
  quantity: number;
  notes?: string | null;
  tags: string[];
  roomId?: number | null;
  furnitureId?: number | null;
  containerId?: number | null;
}

export interface StatsSummary {
  totalItems: number;
  totalValue: number;
  byRoom: { roomId: number; roomName: string; count: number }[];
  recent: { id: number; name: string; createdAt: string }[];
}

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
