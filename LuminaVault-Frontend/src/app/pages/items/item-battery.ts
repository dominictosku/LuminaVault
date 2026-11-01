/**
 * Pure calculation helpers for lithium battery check intervals and maintenance status.
 */
export interface BatteryStatus {
  overdue: boolean;
  nextDue: Date;
  diffDays: number;
}

export function calculateBatteryStatus(
  hasLithiumBattery: boolean,
  intervalMonths: number | null | undefined,
  lastChargedAt: string | null | undefined,
  purchaseDate: string | null | undefined,
  createdAt: string,
  now: Date = new Date()
): BatteryStatus | null {
  if (!hasLithiumBattery) return null;
  const interval = Math.max(1, intervalMonths ?? 3);
  const baseline = lastChargedAt
    ? new Date(lastChargedAt)
    : (purchaseDate ? new Date(purchaseDate) : new Date(createdAt));

  const nextDue = new Date(baseline);
  nextDue.setUTCMonth(nextDue.getUTCMonth() + interval);

  const today = new Date(now);
  today.setUTCHours(0, 0, 0, 0);
  const nextDueDate = new Date(nextDue);
  nextDueDate.setUTCHours(0, 0, 0, 0);

  const overdue = today.getTime() >= nextDueDate.getTime();
  const diffDays = Math.round(Math.abs(today.getTime() - nextDueDate.getTime()) / (1000 * 60 * 60 * 24));

  return {
    overdue,
    nextDue,
    diffDays,
  };
}
