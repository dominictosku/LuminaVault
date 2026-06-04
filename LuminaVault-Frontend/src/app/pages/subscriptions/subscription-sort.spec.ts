import { describe, expect, it } from 'vitest';
import { Subscription } from '../../core/models';
import {
  DEFAULT_SORT,
  defaultDirFor,
  parseSort,
  sortSubscriptions,
  sortToken,
} from './subscription-sort';

function sub(partial: Partial<Subscription>): Subscription {
  return {
    id: 1,
    name: 'Sub',
    category: 'General',
    provider: null,
    accountId: null,
    accountName: null,
    amount: 10,
    currency: 'CHF',
    billingIntervalUnit: 'Month',
    billingIntervalCount: 1,
    billingIntervalDays: 30,
    startedOn: '2026-01-01T00:00:00Z',
    nextDueOn: '2026-06-01T00:00:00Z',
    autoRenew: true,
    status: 'Active',
    notes: null,
    monthlyAmount: 10,
    attachments: [],
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...partial,
  };
}

const names = (list: Subscription[]) => list.map(s => s.name);

describe('parseSort', () => {
  it('parses the current {column}-{dir} tokens', () => {
    expect(parseSort('monthly-desc')).toEqual({ column: 'monthly', dir: 'desc' });
    expect(parseSort('name-asc')).toEqual({ column: 'name', dir: 'asc' });
    expect(parseSort('billing-asc')).toEqual({ column: 'billing', dir: 'asc' });
  });

  it('migrates legacy enum tokens from old URLs / presets', () => {
    expect(parseSort('dueDesc')).toEqual({ column: 'due', dir: 'desc' });
    expect(parseSort('monthlyDesc')).toEqual({ column: 'monthly', dir: 'desc' });
    expect(parseSort('statusAsc')).toEqual({ column: 'status', dir: 'asc' });
  });

  it('falls back to the default for empty / unknown input', () => {
    expect(parseSort(null)).toEqual(DEFAULT_SORT);
    expect(parseSort('')).toEqual(DEFAULT_SORT);
    expect(parseSort('bogus')).toEqual(DEFAULT_SORT);
    expect(parseSort('price-sideways')).toEqual(DEFAULT_SORT);
  });

  it('round-trips with sortToken', () => {
    expect(parseSort(sortToken({ column: 'account', dir: 'desc' }))).toEqual({ column: 'account', dir: 'desc' });
  });
});

describe('defaultDirFor', () => {
  it('defaults amount columns to descending, others ascending', () => {
    expect(defaultDirFor('price')).toBe('desc');
    expect(defaultDirFor('monthly')).toBe('desc');
    expect(defaultDirFor('name')).toBe('asc');
    expect(defaultDirFor('due')).toBe('asc');
  });
});

describe('sortSubscriptions', () => {
  it('sorts by monthly amount in both directions', () => {
    const list = [sub({ name: 'A', monthlyAmount: 5 }), sub({ name: 'B', monthlyAmount: 50 }), sub({ name: 'C', monthlyAmount: 20 })];
    expect(names(sortSubscriptions(list, { column: 'monthly', dir: 'desc' }))).toEqual(['B', 'C', 'A']);
    expect(names(sortSubscriptions(list, { column: 'monthly', dir: 'asc' }))).toEqual(['A', 'C', 'B']);
  });

  it('sorts by name case-insensitively (localeCompare)', () => {
    const list = [sub({ name: 'banana' }), sub({ name: 'Apple' }), sub({ name: 'cherry' })];
    expect(names(sortSubscriptions(list, { column: 'name', dir: 'asc' }))).toEqual(['Apple', 'banana', 'cherry']);
  });

  it('sorts by next due date', () => {
    const list = [
      sub({ name: 'late', nextDueOn: '2026-08-01T00:00:00Z' }),
      sub({ name: 'soon', nextDueOn: '2026-06-01T00:00:00Z' }),
    ];
    expect(names(sortSubscriptions(list, { column: 'due', dir: 'asc' }))).toEqual(['soon', 'late']);
  });

  it('sorts by billing cycle length (day < week < month < year, scaled by count)', () => {
    const list = [
      sub({ name: 'yearly', billingIntervalUnit: 'Year', billingIntervalCount: 1 }),
      sub({ name: 'weekly', billingIntervalUnit: 'Week', billingIntervalCount: 1 }),
      sub({ name: 'biMonthly', billingIntervalUnit: 'Month', billingIntervalCount: 2 }),
    ];
    expect(names(sortSubscriptions(list, { column: 'billing', dir: 'asc' }))).toEqual(['weekly', 'biMonthly', 'yearly']);
  });

  it('orders status Active → Paused → Cancelled', () => {
    const list = [sub({ name: 'c', status: 'Cancelled' }), sub({ name: 'a', status: 'Active' }), sub({ name: 'p', status: 'Paused' })];
    expect(names(sortSubscriptions(list, { column: 'status', dir: 'asc' }))).toEqual(['a', 'p', 'c']);
  });

  it('does not mutate the input array', () => {
    const list = [sub({ name: 'B' }), sub({ name: 'A' })];
    const before = names(list);
    sortSubscriptions(list, { column: 'name', dir: 'asc' });
    expect(names(list)).toEqual(before);
  });
});
