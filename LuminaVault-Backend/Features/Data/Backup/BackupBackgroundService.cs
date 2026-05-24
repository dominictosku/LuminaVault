using LuminaVault.Data;

namespace LuminaVault.Endpoints;

internal class BackupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupBackgroundService> _logger;

    public BackupBackgroundService(
        IServiceScopeFactory scopeFactory,
        BackupOptions options,
        ILogger<BackupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Scheduled backups are disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.IntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var backup = scope.ServiceProvider.GetRequiredService<BackupService>();
                var path = await backup.WriteScheduledBackup(db, _options, stoppingToken);
                _logger.LogInformation("Scheduled backup written to {Path}", path);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled backup failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
