import { BillingIntervalUnit, Subscription, SubscriptionStatus } from '../../core/models';

/// Sort model for the subscriptions list. A single column + direction, kept pure and
/// separate from the component so the comparator, the legacy-token parsing, and the
/// default-direction rules can be reasoned about and tested on their own.

export type SubscriptionSortColumn =
  | 'name' | 'category' | 'account' | 'status' | 'billing' | 'price' | 'monthly' | 'due';
export type SubscriptionSortDir = 'asc' | 'desc';
export interface SubscriptionSortState {
  column: SubscriptionSortColumn;
  dir: SubscriptionSortDir;
}

export const DEFAULT_SORT: SubscriptionSortState = { column: 'due', dir: 'asc' };

export const SORT_COLUMN_LABELS: Record<SubscriptionSortColumn, string> = {
  name: 'Name', category: 'Category', account: 'Account', status: 'Status',
  billing: 'Billing', price: 'Price', monthly: 'Monthly', due: 'Due',
};

/// Numbers/amounts default to descending ("highest first"); everything else ascending.
/// Due stays ascending so the default "soonest first" feel is preserved.
export function defaultDirFor(column: SubscriptionSortColumn): SubscriptionSortDir {
  return column === 'price' || column === 'monthly' ? 'desc' : 'asc';
}

/// Serialize/parse the "{column}-{dir}" token used by the dropdown, URL, and presets.
export function sortToken(sort: SubscriptionSortState): string {
  return `${sort.column}-${sort.dir}`;
}

const LEGACY_TOKENS: Record<string, SubscriptionSortState> = {
  dueAsc: { column: 'due', dir: 'asc' },
  dueDesc: { column: 'due', dir: 'desc' },
  monthlyAsc: { column: 'monthly', dir: 'asc' },
  monthlyDesc: { column: 'monthly', dir: 'desc' },
  nameAsc: { column: 'name', dir: 'asc' },
  categoryAsc: { column: 'category', dir: 'asc' },
  statusAsc: { column: 'status', dir: 'asc' },
};

/// Parse a sort token. Accepts the current "{column}-{dir}" form and the legacy enum
/// tokens that older URLs / saved presets may still carry. Falls back to the default.
export function parseSort(value: string | null | undefined): SubscriptionSortState {
  if (value && LEGACY_TOKENS[value]) return LEGACY_TOKENS[value];
  const match = /^(name|category|account|status|billing|price|monthly|due)-(asc|desc)$/.exec(value ?? '');
  if (match) return { column: match[1] as SubscriptionSortColumn, dir: match[2] as SubscriptionSortDir };
  return { ...DEFAULT_SORT };
}

/// Sort a copy of the list by the given column + direction.
export function sortSubscriptions(
  list: readonly Subscription[], sort: SubscriptionSortState,
): Subscription[] {
  const mul = sort.dir === 'asc' ? 1 : -1;
  return [...list].sort((a, b) => mul * compareColumn(a, b, sort.column));
}

/// Ascending comparator for a single column; direction is applied by the caller. Ties
/// fall back to name (and due for status) so ordering stays stable and sensible.
export function compareColumn(a: Subscription, b: Subscription, column: SubscriptionSortColumn): number {
  switch (column) {
    case 'name': return a.name.localeCompare(b.name);
    case 'category': return a.category.localeCompare(b.category) || a.name.localeCompare(b.name);
    case 'account': return (a.accountName ?? '').localeCompare(b.accountName ?? '') || a.name.localeCompare(b.name);
    case 'status': return statusRank(a.status) - statusRank(b.status) || dateMs(a.nextDueOn) - dateMs(b.nextDueOn);
    case 'billing': return intervalDays(a) - intervalDays(b) || a.name.localeCompare(b.name);
    case 'price': return a.amount - b.amount;
    case 'monthly': return a.monthlyAmount - b.monthlyAmount;
    case 'due':
    default: return dateMs(a.nextDueOn) - dateMs(b.nextDueOn);
  }
}

const INTERVAL_DAYS: Record<BillingIntervalUnit, number> = { Day: 1, Week: 7, Month: 30, Year: 365 };

/// Approximate length of one billing cycle in days, for sorting the Billing column.
function intervalDays(s: Subscription): number {
  return (INTERVAL_DAYS[s.billingIntervalUnit] ?? 30) * Math.max(1, s.billingIntervalCount || 1);
}

function dateMs(value: string): number {
  return new Date(value).getTime();
}

function statusRank(status: SubscriptionStatus): number {
  return status === 'Active' ? 0 : status === 'Paused' ? 1 : 2;
}
