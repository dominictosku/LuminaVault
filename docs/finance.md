# 💰 Personal Finance Suite

LuminaVault includes a double-entry inspired personal finance system tailored for privacy-first, self-hosted wealth tracking and day-to-day money management.

---

## 1. Accounts & Multi-Currency Ledger

- **Account Types**: Checking, Savings, Cash, Credit Card, Investment, Crypto, and Loans.
- **Starting Balance**: Configurable initial balance per account.
- **Multi-Currency Support**: Each account operates in its native currency (e.g. CHF, EUR, USD, GBP, BTC, ETH).
- **Dated Exchange Rates**: Set historical and manual exchange rates in **Settings → Exchange rates**. Dashboard and statistics totals convert balances and transactions into CHF aggregate values using the rate effective on the relevant date.

---

## 2. Transactions & Splits

### Transaction Types
- **Income / Expense**: Standard money movements with payee, category, notes, and tags.
- **Transfer**: Linked money movements between two accounts with no category requirement.
- **Trades (Buy / Sell / Dividend / Fee)**: Dedicated transaction kinds for investment and crypto accounts, tracking `Symbol`, `Quantity`, and `PricePerUnit`.

### Transaction Splits
A single receipt or payment can be split across multiple categories:
- *Example*: A CHF 100 grocery receipt split into CHF 70 `Food` + CHF 30 `Household`.
- Cash impact is driven by the single total amount on the account.
- Analytics and budget progress buckets each split amount into its respective category.
- Splits round-trip in ODS spreadsheets formatted inline as `Category=Amount[|Notes];...`.

### Category Rules
Configure automated rules in **Settings → Category rules** to auto-tag transactions:
- Match on payee, description, or notes using exact text or pattern matching.
- Run in prioritized order.
- Applies to manual saves and bank CSV imports without overwriting explicit categories.

---

## 3. Holdings & Live Price Providers

Track stock, ETF, and cryptocurrency positions per account:
- **Metrics**: Average cost basis, last price, current market value, allocation share, and unrealized profit/loss.
- **Realized P&L**: Computed by rolling forward buy/sell history.
- **Price Providers**:
  - **Finnhub** (Stocks & ETFs): Optional free API key (`LUMINA_FINNHUB_KEY`). For non-US tickers, use exchange suffixes (e.g., `NESN.SW` for Swiss, `SAP.DE` for German).
  - **CoinGecko** (Crypto): Free public API. Use coin identifiers (e.g., `bitcoin`, `ethereum`) for ambiguous tickers.
- **Rate Limiting**: Price refreshes are limited to 6 requests/minute per session to conserve provider quotas.

---

## 4. Budgets & Savings Goals

- **Monthly Budgets**: Set spending limits per category. Progress bars visually track actual spend against limits, and overruns trigger notification warnings.
- **Savings Goals**: Set target funds or debt pay-off plans with target amounts, current balances, and target dates.

---

## 5. Loan Amortisation

Track mortgages, personal loans, vehicle financing, and educational debts:
- Input principal, annual interest rate, term length, start date, and optional extra monthly payments.
- Detailed amortization schedules display monthly principal/interest breakdowns.
- Visual summary calculates the total interest paid and early payoff date achieved through extra contributions.

---

## 6. Subscriptions & Automation

Manage recurring contracts, SaaS subscriptions, insurance, and memberships:
- **Flexible Billing Intervals**: Calendar-aware recurrence (every N days, weeks, months, or years).
- **Normalized Monthly Cost**: Calculates true monthly impact based on calendar units (e.g., CHF 120/year divides to CHF 10/month).
- **Month Boundary Awareness**: Correctly rolls forward across month end dates (e.g., Jan 31 → Feb 28 → Mar 31).
- **Auto-Forecast Generation**: The background worker can automatically generate pending forecast transactions 7 days ahead for active subscriptions:
  ```jsonc
  // LuminaVault-Backend/appsettings.json
  {
    "SubscriptionAutomation": {
      "Enabled": true,
      "LookAheadDays": 7,
      "IntervalHours": 24
    }
  }
  ```

---

## 7. Cash-Flow Forecast

Located at `/forecast`, this tool projects future liquid cash 14, 30, 60, 90, or 180 days forward:
- Replays known pending transactions and scheduled subscription dues rolled forward by their billing intervals.
- Eliminates trend guesswork: every point on the chart corresponds to a tangible scheduled event.
- Displays lowest projected point, warning banner if liquidity dips below zero, and event timeline breakdown.

---

## 8. Net Worth History

Daily snapshots recorded by `NetWorthBackgroundService`:
- Aggregates liquid cash + investment market values + inventory asset values into the base currency.
- Visualized as an interactive line chart on the dashboard with performance delta badges.
- Immediate manual snapshot trigger via `POST /api/finance/net-worth/snapshot` or the "Snapshot now" dashboard button.

```jsonc
{
  "NetWorth": {
    "Enabled": true,
    "IntervalHours": 24,
    "RetentionDays": 0 // 0 = keep forever
  }
}
```

---

## 9. Swiss Tax Export

Year-end bundle for Swiss tax filings (Wertschriftenverzeichnis and wealth declaration):
- **Holdings as of Dec 31**: Derived from trade history.
- **Cash Account Balances**: Position balances on Dec 31.
- **Annual Income & Expense Summaries**: Categorized totals.
- **Pricing Sanity Check**: Identifies rows where the last known price is older than Dec 31 so manual overrides can be applied before export.
- Exportable as on-screen interactive tables or multi-sheet ODS spreadsheet.
