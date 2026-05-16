namespace LuminaVault.Domain;

/// One row per day captures the aggregate net worth, stored in the base currency.
/// Used to draw the historical equity curve on the dashboard without re-running
/// the full summary computation for every past point.
public class NetWorthSnapshot
{
    public int Id { get; set; }
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow.Date;
    public decimal AccountNetWorth { get; set; }
    public decimal HoldingsMarketValue { get; set; }
    public decimal HoldingsCostBasis { get; set; }
    public decimal InventoryValue { get; set; }
    public decimal NetWorth { get; set; }
    public string Currency { get; set; } = "CHF";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
