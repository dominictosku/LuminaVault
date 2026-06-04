import { describe, expect, it } from 'vitest';
import { SplitAmount, isSplitBalanced, remainingToAllocate, sumSplits } from './split-math';

// ngModel feeds numbers through as strings, so the helpers must coerce.
const splits = (...amounts: (number | string)[]): SplitAmount[] =>
  amounts.map(amount => ({ amount: amount as number }));

describe('sumSplits', () => {
  it('adds numeric amounts', () => {
    expect(sumSplits(splits(70, 20, 10))).toBe(100);
  });

  it('coerces string amounts and treats blank/non-numeric rows as 0', () => {
    expect(sumSplits(splits('70', '', 'abc', 30))).toBe(100);
  });

  it('is 0 for no splits', () => {
    expect(sumSplits([])).toBe(0);
  });
});

describe('remainingToAllocate', () => {
  it('is the transaction amount minus what is allocated', () => {
    expect(remainingToAllocate(100, splits(70, 20))).toBe(10);
  });

  it('rounds to rappen so floating-point noise never leaves a phantom remainder', () => {
    // 0.1 + 0.1 + 0.1 === 0.30000000000000004 in IEEE-754; rounding can land on -0,
    // which is harmless (Math.abs in isSplitBalanced / Math.max(0, …) in the form).
    expect(Math.abs(remainingToAllocate(0.3, splits(0.1, 0.1, 0.1)))).toBe(0);
  });

  it('reports an over-allocation as a negative remainder', () => {
    expect(remainingToAllocate(100, splits(70, 50))).toBe(-20);
  });

  it('coerces a string amount from the form', () => {
    expect(remainingToAllocate('100' as unknown as number, splits(40))).toBe(60);
  });
});

describe('isSplitBalanced', () => {
  it('treats anything within half a rappen as balanced', () => {
    expect(isSplitBalanced(0)).toBe(true);
    expect(isSplitBalanced(0.004)).toBe(true);
    expect(isSplitBalanced(-0.004)).toBe(true);
  });

  it('rejects a remainder of half a rappen or more', () => {
    expect(isSplitBalanced(0.005)).toBe(false);
    expect(isSplitBalanced(0.01)).toBe(false);
    expect(isSplitBalanced(-1)).toBe(false);
  });
});
