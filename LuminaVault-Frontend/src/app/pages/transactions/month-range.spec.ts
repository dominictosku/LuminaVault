import { describe, expect, it } from 'vitest';
import { monthToRange } from './month-range';

describe('monthToRange', () => {
  it('expands a valid YYYY-MM into a full-month range', () => {
    expect(monthToRange('2026-06')).toEqual({ from: '2026-06-01', to: '2026-06-30' });
  });

  it('uses the correct last day for 31-day and February months', () => {
    expect(monthToRange('2026-01').to).toBe('2026-01-31');
    expect(monthToRange('2026-02').to).toBe('2026-02-28');
  });

  it('handles leap-year February', () => {
    expect(monthToRange('2024-02').to).toBe('2024-02-29');
  });

  it('returns {} for empty / nullish input (no filter)', () => {
    expect(monthToRange('')).toEqual({});
    expect(monthToRange(null)).toEqual({});
    expect(monthToRange(undefined)).toEqual({});
  });

  it('returns {} for malformed input instead of producing a bad date', () => {
    // Firefox renders type=month as text, so users can type junk like "1".
    // The old code turned "1" into to="1-NaN" and the API rejected it.
    expect(monthToRange('1')).toEqual({});
    expect(monthToRange('2026')).toEqual({});
    expect(monthToRange('2026-6')).toEqual({}); // unpadded month
    expect(monthToRange('abc')).toEqual({});
    expect(monthToRange('2026-13')).toEqual({}); // month out of range
    expect(monthToRange('2026-00')).toEqual({});
  });

  it('trims surrounding whitespace', () => {
    expect(monthToRange('  2026-06  ')).toEqual({ from: '2026-06-01', to: '2026-06-30' });
  });
});
