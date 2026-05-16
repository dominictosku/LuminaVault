namespace LuminaVault.Endpoints;

public class NetWorthOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 24;
    public int RetentionDays { get; set; } = 0; // 0 = keep forever
}
