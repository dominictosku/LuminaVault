/// Pure split-balancing math for the transaction form. A split-enabled Income/Expense
/// divides one Amount across several category buckets; these helpers track how much is
/// allocated vs. still outstanding so the form can validate and auto-fill rows. Kept
/// separate from the component because this is the correctness-sensitive part (the cash
/// impact must equal the sum of splits) and it's worth testing on its own.

export interface SplitAmount {
  /// Typed number, but ngModel hands values through as strings — every reader coerces.
  amount: number;
}

/// Sum of allocated split amounts. Non-numeric/blank rows count as 0.
export function sumSplits(splits: readonly SplitAmount[]): number {
  return splits.reduce((sum, s) => sum + (Number(s.amount) || 0), 0);
}

/// Amount still to allocate (transaction amount − allocated), rounded to rappen so
/// floating-point noise doesn't leave a phantom 0.001 remainder.
export function remainingToAllocate(amount: number, splits: readonly SplitAmount[]): number {
  return Number(((Number(amount) || 0) - sumSplits(splits)).toFixed(2));
}

/// Whether the splits sum to the transaction amount within half a rappen.
export function isSplitBalanced(remaining: number): boolean {
  return Math.abs(remaining) < 0.005;
}
