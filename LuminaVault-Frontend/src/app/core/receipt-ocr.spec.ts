import { describe, expect, it } from 'vitest';
import { parseReceipt } from './receipt-ocr';

describe('parseReceipt', () => {
  it('extracts merchant, amount and date from a Swiss supermarket receipt', () => {
    const ocr = [
      'MIGROS GENOSSENSCHAFT',
      'Filiale Bahnhof Bern',
      '17.05.2026 14:32',
      '',
      'Brot           3.20',
      'Milch          1.95',
      'Käse           8.40',
      '',
      'TOTAL CHF      13.55',
      'Bar            20.00',
      'Retour          6.45',
    ].join('\n');
    const r = parseReceipt(ocr);
    expect(r.payee).toBe('MIGROS GENOSSENSCHAFT');
    expect(r.amount).toBe(13.55);
    expect(r.date).toBe('2026-05-17');
  });

  it('honours TOTAL over subtotals and discounts on the same receipt', () => {
    const ocr = [
      'COOP CITY',
      'Subtotal       42.00',
      'Discount       -2.00',
      'Total          40.00',
    ].join('\n');
    const r = parseReceipt(ocr);
    // The Total line wins because TOTAL_KEYWORDS is consulted before the largest-number fallback.
    expect(r.amount).toBe(40.0);
  });

  it('parses Swiss apostrophe-separated thousands', () => {
    const ocr = ['BUILDER AG', 'Total CHF      1\'234.50'].join('\n');
    const r = parseReceipt(ocr);
    expect(r.amount).toBe(1234.5);
  });

  it('parses EU comma decimal notation', () => {
    const ocr = ['REWE', 'Gesamtbetrag   12,40'].join('\n');
    const r = parseReceipt(ocr);
    expect(r.amount).toBe(12.4);
  });

  it('falls back to the largest 2dp number when no total keyword is present', () => {
    const ocr = ['Some Shop', 'Item A 5.00', 'Item B 12.30', 'Item C 2.50'].join('\n');
    const r = parseReceipt(ocr);
    expect(r.amount).toBe(12.3);
  });

  it('handles ISO dates anywhere in the document', () => {
    const ocr = ['Online Store', 'Order date: 2026-05-17', 'Total       9.99'].join('\n');
    expect(parseReceipt(ocr).date).toBe('2026-05-17');
  });

  it('rejects impossible dates (Feb 30) and continues searching', () => {
    const ocr = ['Vendor', '30.02.2026 invoice ref', 'Issued 17.05.2026', 'Total 5.00'].join('\n');
    expect(parseReceipt(ocr).date).toBe('2026-05-17');
  });

  it('skips header-noise lines when guessing the payee', () => {
    const ocr = ['QUITTUNG', 'KASSENBELEG', 'BÄCKEREI ANNA', '17.05.2026', 'Total 4.20'].join('\n');
    expect(parseReceipt(ocr).payee).toBe('BÄCKEREI ANNA');
  });

  it('strips trailing terminal/cashier ids from the payee line', () => {
    const ocr = ['MIGROS *#1234', '17.05.2026', 'Total 10.00'].join('\n');
    expect(parseReceipt(ocr).payee).toBe('MIGROS');
  });

  it('returns nulls on garbage input rather than throwing', () => {
    const r = parseReceipt('!!! @@@\n\n  ???');
    expect(r.payee).toBeNull();
    expect(r.amount).toBeNull();
    expect(r.date).toBeNull();
  });
});
