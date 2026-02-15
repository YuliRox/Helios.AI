using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Alarm.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LumiRise.Api.Services.Alarm.Implementation;

public sealed class AlarmRecurringJobSyncHostedService : BackgroundService
{
    private readonly IAlarmRecurringJobSynchronizer _synchronizer;
    private readonly AlarmSchedulerOptions _options;
    private readonly ILogger<AlarmRecurringJobSyncHostedService> _logger;

    public AlarmRecurringJobSyncHostedService(
        IAlarmRecurringJobSynchronizer synchronizer,
        IOptions<AlarmSchedulerOptions> options,
        ILogger<AlarmRecurringJobSyncHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(synchronizer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _synchronizer = synchronizer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(5, _options.SyncIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        await RunSyncAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            await _synchronizer.SyncAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync alarm recurring jobs from database");
        }
    }
}
