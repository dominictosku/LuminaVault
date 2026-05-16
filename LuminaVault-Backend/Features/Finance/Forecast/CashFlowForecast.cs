namespace LuminaVault.Endpoints;

/// One row per day in the forecast window. Balance is the running total of liquid cash
/// across all included accounts, converted to the base currency at current rates.
public record ForecastPoint(DateTime Date, decimal Balance, decimal ChangeFromYesterday);

/// A single known event projected into the future. `Amount` is in the source account's
/// currency (positive in, negative out); `BaseAmount` is the same in base currency.
public record ForecastEvent(
    DateTime Date,
    string Source,             // "Subscription" | "Pending" | "Transfer"
    string Description,
    int? AccountId,
    string? AccountName,
    decimal Amount,
    string Currency,
    decimal BaseAmount);

public record CashFlowForecast(
    DateTime GeneratedAt,
    DateTime From,
    DateTime To,
    int Days,
    string BaseCurrency,
    decimal StartingBalance,
    decimal EndingBalance,
    decimal NetChange,
    decimal LowestBalance,
    DateTime LowestDate,
    int EventCount,
    ForecastPoint[] Daily,
    ForecastEvent[] Events);
