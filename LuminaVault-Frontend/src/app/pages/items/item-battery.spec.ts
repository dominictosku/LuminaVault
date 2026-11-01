import { describe, expect, it } from 'vitest';
import { calculateBatteryStatus } from './item-battery';

describe('calculateBatteryStatus', () => {
  it('returns null when item does not have a lithium battery', () => {
    const status = calculateBatteryStatus(false, 3, null, null, '2026-01-01T00:00:00Z');
    expect(status).toBeNull();
  });

  it('calculates due date 3 months after last charged date by default', () => {
    const now = new Date('2026-04-15T12:00:00Z');
    const lastCharged = '2026-01-01T00:00:00Z';
    const status = calculateBatteryStatus(true, 3, lastCharged, null, '2025-01-01T00:00:00Z', now);

    expect(status).not.toBeNull();
    expect(status!.overdue).toBe(true);
    expect(status!.nextDue.toISOString().startsWith('2026-04-01')).toBe(true);
    expect(status!.diffDays).toBe(14);
  });

  it('reports not overdue when within the 3 month interval', () => {
    const now = new Date('2026-02-01T12:00:00Z');
    const lastCharged = '2026-01-01T00:00:00Z';
    const status = calculateBatteryStatus(true, 3, lastCharged, null, '2025-01-01T00:00:00Z', now);

    expect(status).not.toBeNull();
    expect(status!.overdue).toBe(false);
  });

  it('supports custom charge intervals like 6 months', () => {
    const now = new Date('2026-05-01T12:00:00Z');
    const lastCharged = '2026-01-01T00:00:00Z';
    const status = calculateBatteryStatus(true, 6, lastCharged, null, '2025-01-01T00:00:00Z', now);

    expect(status).not.toBeNull();
    expect(status!.overdue).toBe(false);
    expect(status!.nextDue.toISOString().startsWith('2026-07-01')).toBe(true);
  });

  it('falls back to purchase date or creation date when not charged yet', () => {
    const now = new Date('2026-05-01T12:00:00Z');
    const purchaseDate = '2026-01-01T00:00:00Z';
    const status = calculateBatteryStatus(true, 3, null, purchaseDate, '2026-01-01T00:00:00Z', now);

    expect(status).not.toBeNull();
    expect(status!.overdue).toBe(true);
  });
});
