using LuminaVault.Data;

namespace LuminaVault.Endpoints;

internal class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationBackgroundService> _logger;

    public NotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        NotificationOptions options,
        ILogger<NotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Notification scanner is disabled.");
            return;
        }

        // Brief startup grace so we don't fire alerts mid-migration on a cold boot.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.IntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scanner = scope.ServiceProvider.GetRequiredService<NotificationScanner>();
                var touched = await scanner.ScanAllAsync(db, stoppingToken);
                if (touched > 0)
                    _logger.LogInformation("Notification scan upserted {Count} alert(s)", touched);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification scan failed.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
