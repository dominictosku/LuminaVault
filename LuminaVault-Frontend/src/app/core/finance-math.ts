import { Holding, Subscription } from './models';

/// Per-account aggregation of holdings: portfolio view in the Holdings page,
/// summary chips on the dashboard, etc. The math is sufficiently load-bearing
/// (it backs the displayed net worth and P&L) to live in its own testable module.
export interface HoldingsAccountGroup {
  accountId: number;
  accountName: string;
  currency: string;
  holdings: Holding[];
  costBasis: number;
  marketValue: number;
  unrealizedPnL: number;
  totalReturn: number;
}

export interface HoldingsTotals {
  costBasis: number;
  marketValue: number;
  pnl: number;
  pnlPercent: number;
  totalReturn: number;
}

/// Group holdings by account, summing costBasis / marketValue / unrealizedPnL.
/// Falls back to costBasis when marketValue isn't available (no price provider
/// configured yet, or stale data) so the totals stay non-misleading. Sorted by
/// account name for stable rendering.
export function aggregateHoldingsByAccount(holdings: Iterable<Holding>): HoldingsAccountGroup[] {
  const map = new Map<number, HoldingsAccountGroup>();
  for (const h of holdings) {
    let group = map.get(h.accountId);
    if (!group) {
      group = {
        accountId: h.accountId,
        accountName: h.accountName ?? 'Account',
        currency: h.currency,
        holdings: [],
        costBasis: 0,
        marketValue: 0,
        unrealizedPnL: 0,
        totalReturn: 0,
      };
      map.set(h.accountId, group);
    }
    group.holdings.push(h);
    group.costBasis += h.costBasis;
    group.marketValue += h.marketValue ?? h.costBasis;
    group.unrealizedPnL += h.unrealizedPnL ?? 0;
    group.totalReturn += h.totalReturn ?? 0;
  }
  return Array.from(map.values()).sort((a, b) => a.accountName.localeCompare(b.accountName));
}

/// Roll up a list of account groups into a single totals row. `pnlPercent`
/// guards against div-by-zero when there are no holdings or cost basis is 0.
export function holdingsTotals(groups: Iterable<HoldingsAccountGroup>): HoldingsTotals {
  let costBasis = 0;
  let marketValue = 0;
  let totalReturn = 0;
  for (const g of groups) {
    costBasis += g.costBasis;
    marketValue += g.marketValue;
    totalReturn += g.totalReturn;
  }
  const pnl = marketValue - costBasis;
  const pnlPercent = costBasis > 0 ? (pnl / costBasis) * 100 : 0;
  return { costBasis, marketValue, pnl, pnlPercent, totalReturn };
}

/// Monthly cost of the currently-active subscriptions. Cancelled/Paused are excluded;
/// monthlyAmount is pre-normalised by the backend (annual subscriptions / 12, etc.).
export function activeSubscriptionsMonthlyTotal(subscriptions: Iterable<Subscription>): number {
  let sum = 0;
  for (const s of subscriptions) {
    if (s.status === 'Active') sum += s.monthlyAmount;
  }
  return sum;
}

/// Derive the cash impact of a trade from quantity and per-unit price. Used by the
/// transaction form to auto-fill the amount field when the user types qty/price.
/// Returns 0 when either input is non-positive (the form treats 0 as "don't auto-fill").
export function tradeAmount(quantity: number | null | undefined, pricePerUnit: number | null | undefined): number {
  const qty = Number(quantity) || 0;
  const price = Number(pricePerUnit) || 0;
  if (qty <= 0 || price <= 0) return 0;
  return Number((qty * price).toFixed(2));
}
