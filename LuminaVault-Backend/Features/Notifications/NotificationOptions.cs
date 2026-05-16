namespace LuminaVault.Endpoints;

public class NotificationOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 6;
    public int SubscriptionDueLookAheadDays { get; set; } = 3;
    public int ForecastLookAheadDays { get; set; } = 30;
}
