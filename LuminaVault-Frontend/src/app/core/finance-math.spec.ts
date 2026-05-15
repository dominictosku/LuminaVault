import { describe, expect, it } from 'vitest';
import {
  activeSubscriptionsMonthlyTotal,
  aggregateHoldingsByAccount,
  holdingsTotals,
  tradeAmount,
} from './finance-math';
import { Holding, Subscription } from './models';

function holding(partial: Partial<Holding>): Holding {
  return {
    id: 1,
    accountId: 1,
    accountName: 'Brokerage',
    currency: 'CHF',
    symbol: 'VTI',
    name: null,
    quantity: 10,
    averageCost: 100,
    lastPrice: null,
    lastPriceAt: null,
    providerId: null,
    costBasis: 1000,
    marketValue: null,
    unrealizedPnL: null,
    unrealizedPnLPercent: null,
    notes: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...partial,
  };
}

function subscription(partial: Partial<Subscription>): Subscription {
  return {
    id: 1,
    name: 'Netflix',
    category: 'Subscriptions',
    provider: null,
    accountId: 1,
    accountName: 'Checking',
    amount: 17.9,
    currency: 'CHF',
    billingIntervalDays: 30,
    startedOn: '2026-01-01',
    nextDueOn: '2026-02-01',
    autoRenew: true,
    status: 'Active',
    notes: null,
    monthlyAmount: 17.9,
    attachments: [],
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...partial,
  };
}

describe('aggregateHoldingsByAccount', () => {
  it('groups holdings by account and sums cost basis / market value', () => {
    const groups = aggregateHoldingsByAccount([
      holding({ id: 1, accountId: 1, accountName: 'Broker A', costBasis: 1000, marketValue: 1200, unrealizedPnL: 200 }),
      holding({ id: 2, accountId: 1, accountName: 'Broker A', costBasis: 500, marketValue: 450, unrealizedPnL: -50 }),
      holding({ id: 3, accountId: 2, accountName: 'Broker B', costBasis: 2000, marketValue: 2500, unrealizedPnL: 500 }),
    ]);

    expect(groups).toHaveLength(2);
    const a = groups.find(g => g.accountId === 1)!;
    expect(a.costBasis).toBe(1500);
    expect(a.marketValue).toBe(1650);
    expect(a.unrealizedPnL).toBe(150);
    expect(a.holdings).toHaveLength(2);

    const b = groups.find(g => g.accountId === 2)!;
    expect(b.costBasis).toBe(2000);
    expect(b.marketValue).toBe(2500);
  });

  it('falls back to cost basis when market value is null (stale or no provider)', () => {
    const groups = aggregateHoldingsByAccount([
      holding({ accountId: 1, costBasis: 800, marketValue: null, unrealizedPnL: null }),
    ]);
    expect(groups[0].marketValue).toBe(800);
    expect(groups[0].unrealizedPnL).toBe(0);
  });

  it('returns an empty array when there are no holdings', () => {
    expect(aggregateHoldingsByAccount([])).toEqual([]);
  });

  it('sorts groups by account name for stable rendering', () => {
    const groups = aggregateHoldingsByAccount([
      holding({ accountId: 3, accountName: 'Zerodha' }),
      holding({ accountId: 1, accountName: 'Alpaca' }),
      holding({ accountId: 2, accountName: 'Merrill' }),
    ]);
    expect(groups.map(g => g.accountName)).toEqual(['Alpaca', 'Merrill', 'Zerodha']);
  });

  it('falls back to "Account" when accountName is null', () => {
    const groups = aggregateHoldingsByAccount([
      holding({ accountId: 1, accountName: null as any }),
    ]);
    expect(groups[0].accountName).toBe('Account');
  });
});

describe('holdingsTotals', () => {
  it('sums across groups and computes P&L percentage', () => {
    const totals = holdingsTotals([
      { accountId: 1, accountName: 'A', currency: 'CHF', holdings: [], costBasis: 1000, marketValue: 1200, unrealizedPnL: 200 },
      { accountId: 2, accountName: 'B', currency: 'CHF', holdings: [], costBasis: 500, marketValue: 600, unrealizedPnL: 100 },
    ]);
    expect(totals.costBasis).toBe(1500);
    expect(totals.marketValue).toBe(1800);
    expect(totals.pnl).toBe(300);
    expect(totals.pnlPercent).toBeCloseTo(20, 5);
  });

  it('returns zero pnlPercent when cost basis is zero (no positions yet)', () => {
    const totals = holdingsTotals([]);
    expect(totals.pnlPercent).toBe(0);
    expect(totals.costBasis).toBe(0);
  });
});

describe('activeSubscriptionsMonthlyTotal', () => {
  it('sums monthlyAmount across active subscriptions only', () => {
    const total = activeSubscriptionsMonthlyTotal([
      subscription({ status: 'Active', monthlyAmount: 17.9 }),
      subscription({ status: 'Active', monthlyAmount: 12.5 }),
      subscription({ status: 'Cancelled', monthlyAmount: 100 }), // excluded
      subscription({ status: 'Paused', monthlyAmount: 50 }),     // excluded
    ]);
    expect(total).toBeCloseTo(30.4, 5);
  });

  it('returns 0 when no subscriptions are active', () => {
    expect(activeSubscriptionsMonthlyTotal([])).toBe(0);
    expect(activeSubscriptionsMonthlyTotal([
      subscription({ status: 'Cancelled', monthlyAmount: 100 }),
    ])).toBe(0);
  });
});

describe('tradeAmount', () => {
  it('multiplies quantity by price and rounds to 2 decimals', () => {
    expect(tradeAmount(10, 100)).toBe(1000);
    expect(tradeAmount(7, 11.11)).toBe(77.77);
    // 3 * 33.333 = 99.999 → .toFixed(2) rounds up to 100.00.
    // Validates that we round to 2 decimals, not arbitrary precision.
    expect(tradeAmount(3, 33.333)).toBe(100);
  });

  it('rounds banker-ish to 2 decimals for fractional shares', () => {
    // 0.123 * 99.99 = 12.298... → 12.3
    expect(tradeAmount(0.123, 99.99)).toBeCloseTo(12.3, 2);
  });

  it('returns 0 when quantity or price is missing, zero, or negative', () => {
    expect(tradeAmount(null, 100)).toBe(0);
    expect(tradeAmount(10, null)).toBe(0);
    expect(tradeAmount(undefined, undefined)).toBe(0);
    expect(tradeAmount(0, 100)).toBe(0);
    expect(tradeAmount(10, 0)).toBe(0);
    expect(tradeAmount(-1, 100)).toBe(0);
  });

  it('coerces string-typed numbers (form inputs come through ngModel as strings)', () => {
    expect(tradeAmount('10' as any, '100' as any)).toBe(1000);
  });
});
