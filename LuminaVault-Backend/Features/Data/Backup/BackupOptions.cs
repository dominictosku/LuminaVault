namespace LuminaVault.Endpoints;

public class BackupOptions
{
    public bool Enabled { get; set; }
    public string Directory { get; set; } = "backups";
    public int IntervalHours { get; set; } = 24;
    public int RetainedFiles { get; set; } = 14;
}
