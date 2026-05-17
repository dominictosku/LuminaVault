/// Best-effort receipt parser: takes the raw OCR'd text from Tesseract and pulls out
/// a merchant guess, the total amount, and the transaction date. Designed for low-noise
/// supermarket / restaurant / petrol receipts; results are *suggestions* meant to pre-fill
/// the form the user is about to review and submit, not an authoritative extraction.
///
/// The Tesseract.js model + WASM (~3 MB) is loaded lazily on first OCR call so the
/// initial bundle isn't penalized for a feature most users invoke rarely.

export interface ParsedReceipt {
  payee: string | null;
  amount: number | null;
  /// `yyyy-MM-dd` if a date was recognised, otherwise null.
  date: string | null;
}

// Words that, when on the same line as a number, strongly suggest that number is the total.
// Order matters loosely — earlier entries win ties because they're the most specific signals.
const TOTAL_KEYWORDS = [
  'total chf', 'total eur', 'total usd', 'totalbetrag',
  'gesamtbetrag', 'gesamtsumme', 'gesamt',
  'rechnungstotal', 'rechnungsbetrag', 'rechnung',
  'betrag', 'summe', 'sum',
  'amount due', 'amount', 'to pay', 'zu zahlen',
  'total', 'ttl',
];

// Lines that should never become the payee — they're metadata noise.
const PAYEE_BLOCKLIST = /^(receipt|kassenbon|quittung|invoice|rechnung|bill|bon|kassenbeleg|terminal|kasse|filiale|store|shop|date|datum|time|uhrzeit|tel|phone|tax|mwst|vat|ust)\b/i;

export function parseReceipt(text: string): ParsedReceipt {
  const rawLines = text.split(/\r?\n/);
  const lines = rawLines.map(l => l.trim()).filter(l => l.length > 0);
  return {
    payee: guessPayee(lines),
    amount: guessAmount(lines),
    date: guessDate(text),
  };
}

function guessPayee(lines: string[]): string | null {
  // Merchants typically print their name on the first 1-3 lines of the receipt.
  // Look at the top 5 lines so a "Kassenbon" header doesn't disqualify the whole receipt,
  // and prefer lines that look like a name (letters, not mostly digits/punctuation).
  for (const line of lines.slice(0, 5)) {
    if (PAYEE_BLOCKLIST.test(line)) continue;
    const letters = (line.match(/[A-Za-zÀ-ÿ]/g) ?? []).length;
    const digits = (line.match(/\d/g) ?? []).length;
    if (letters < 3) continue;        // not a name
    if (digits > letters) continue;   // looks more like a phone number / receipt id
    if (line.length > 60) continue;   // probably an address, not the brand
    return cleanPayee(line);
  }
  return null;
}

function cleanPayee(line: string): string {
  // Strip trailing punctuation/numbers some printers append (e.g. "MIGROS *#1234").
  return line.replace(/[\s\-*#:]*\d+\s*$/, '').trim();
}

function guessAmount(lines: string[]): number | null {
  // First pass: any line that mentions a "total" keyword and contains a number wins,
  // taking the largest matching number on that line. We use word boundaries so
  // "Subtotal" doesn't match "total" — otherwise the first line of a discount-then-total
  // receipt mis-fires on the subtotal value.
  for (const keyword of TOTAL_KEYWORDS) {
    const re = new RegExp(`\\b${escapeRegExp(keyword)}\\b`, 'i');
    for (const line of lines) {
      if (!re.test(line)) continue;
      const candidates = extractNumbers(line);
      if (candidates.length > 0) {
        return Math.max(...candidates);
      }
    }
  }
  // Fallback: largest number with exactly two decimal places anywhere in the document.
  // Real-world subtotals/discounts can outsize the total in degenerate scans, but most
  // of the time the gross total is the biggest 2dp number on a receipt.
  const all: number[] = [];
  for (const line of lines) all.push(...extractNumbers(line));
  if (all.length === 0) return null;
  return Math.max(...all);
}

function escapeRegExp(s: string) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function extractNumbers(line: string): number[] {
  // Matches `12.50`, `12,50`, `1'234.50`, `1,234.50`. Requires two trailing decimal
  // digits so we don't snag stray "EUR" tax IDs or two-digit table numbers.
  const re = /(?:^|[^\d.,'])(\d{1,3}(?:[.,']\d{3})*[.,]\d{2}|\d+[.,]\d{2})(?=$|[^\d.,'])/g;
  const out: number[] = [];
  for (const m of line.matchAll(re)) {
    const normalized = normalizeNumber(m[1]);
    if (normalized != null && normalized > 0 && normalized < 1_000_000) out.push(normalized);
  }
  return out;
}

function normalizeNumber(raw: string): number | null {
  // Strip thousands separators (apostrophe = Swiss, comma = US, period = EU) then
  // pick whichever of `,` or `.` is acting as the decimal point (always the last one).
  let s = raw.replace(/'/g, '');
  const lastComma = s.lastIndexOf(',');
  const lastDot = s.lastIndexOf('.');
  if (lastComma >= 0 && lastDot >= 0) {
    // Whichever appears LAST is the decimal mark; the other is a thousands separator.
    const decimalIsComma = lastComma > lastDot;
    s = decimalIsComma
      ? s.replace(/\./g, '').replace(',', '.')
      : s.replace(/,/g, '');
  } else if (lastComma >= 0) {
    s = s.replace(',', '.');
  }
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
}

function guessDate(text: string): string | null {
  // ISO first: `2026-05-17`.
  const iso = /\b(20\d{2})-(\d{2})-(\d{2})\b/.exec(text);
  if (iso) {
    const [_, y, m, d] = iso;
    if (validDate(+y, +m, +d)) return `${y}-${m}-${d}`;
  }
  // EU short: `17.05.2026`, `17/05/2026`, `17-05-2026`. Two-digit year handled too.
  const eu = /\b(\d{1,2})[.\/-](\d{1,2})[.\/-](\d{2,4})\b/g;
  for (const m of text.matchAll(eu)) {
    const day = +m[1];
    const month = +m[2];
    let year = +m[3];
    if (year < 100) year += 2000;
    if (!validDate(year, month, day)) continue;
    return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  }
  return null;
}

function validDate(year: number, month: number, day: number) {
  if (year < 2000 || year > 2100) return false;
  if (month < 1 || month > 12) return false;
  if (day < 1 || day > 31) return false;
  // Real calendar check: rolls over for impossible combos (Feb 30 → Mar 2).
  const d = new Date(Date.UTC(year, month - 1, day));
  return d.getUTCFullYear() === year && d.getUTCMonth() === month - 1 && d.getUTCDate() === day;
}

/// Lazy wrapper around tesseract.js. The library + English language pack weighs ~3 MB,
/// so we dynamic-import it on first call to keep the initial bundle slim. Subsequent
/// calls reuse the cached module via the bundler's import cache.
export async function recognizeReceipt(
  file: File,
  onProgress?: (fraction: number) => void,
): Promise<string> {
  const { recognize } = await import('tesseract.js');
  const result = await recognize(file, 'eng', {
    logger: msg => {
      if (onProgress && msg.status === 'recognizing text') {
        onProgress(msg.progress ?? 0);
      }
    },
  });
  return result.data.text ?? '';
}
