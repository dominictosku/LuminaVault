using LuminaVault.Data;

namespace LuminaVault.Endpoints;

internal class NetWorthBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NetWorthOptions _options;
    private readonly ILogger<NetWorthBackgroundService> _logger;

    public NetWorthBackgroundService(
        IServiceScopeFactory scopeFactory,
        NetWorthOptions options,
        ILogger<NetWorthBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Net worth snapshots are disabled.");
            return;
        }

        // Wait briefly on boot so the snapshot for "today" reflects steady-state values
        // rather than mid-startup data (e.g. exchange rate seeding still running).
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.IntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<NetWorthSnapshotService>();
                var snapshot = await service.CaptureAsync(db, stoppingToken);
                _logger.LogInformation("Captured net worth snapshot for {Date}: {Amount} {Currency}",
                    snapshot.SnapshotDate.ToString("yyyy-MM-dd"), snapshot.NetWorth, snapshot.Currency);

                if (_options.RetentionDays > 0)
                {
                    var pruned = await service.PruneAsync(db, _options.RetentionDays, stoppingToken);
                    if (pruned > 0)
                        _logger.LogInformation("Pruned {Count} net worth snapshots older than {Days} days", pruned, _options.RetentionDays);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Net worth snapshot capture failed.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
