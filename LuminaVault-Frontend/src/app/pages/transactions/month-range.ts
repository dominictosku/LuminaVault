/// Convert a `YYYY-MM` month-filter value into an inclusive { from, to } date range
/// (ISO `yyyy-MM-dd`) for the transactions query. Returns {} for empty or malformed
/// input so nothing is sent to the backend.
///
/// This guard matters because Firefox renders `<input type="month">` as a plain text
/// field (no native month picker), so a user can type junk like "1". Without the
/// format check that became `to="1-NaN"`, which the API rejected with a
/// "Failed to bind parameter Nullable<DateTime>" error.
export interface DateRange {
  from?: string;
  to?: string;
}

export function monthToRange(value: string | null | undefined): DateRange {
  const match = /^(\d{4})-(\d{2})$/.exec((value ?? '').trim());
  if (!match) return {};
  const [, yyyy, mm] = match;
  const month = Number(mm);
  if (month < 1 || month > 12) return {};
  // Day 0 of the next month is the last day of this one (handles leap years).
  const lastDay = new Date(Number(yyyy), month, 0).getDate();
  return {
    from: `${yyyy}-${mm}-01`,
    to: `${yyyy}-${mm}-${String(lastDay).padStart(2, '0')}`,
  };
}
