using LumiRise.Api.Services.Alarm.Interfaces;
using Microsoft.Extensions.Hosting;

namespace LumiRise.Api.Services.Alarm.Implementation;

public sealed class AlarmRecurringJobSyncHostedService : BackgroundService
{
    private readonly IAlarmRecurringJobSynchronizer _synchronizer;
    private readonly ILogger<AlarmRecurringJobSyncHostedService> _logger;

    public AlarmRecurringJobSyncHostedService(
        IAlarmRecurringJobSynchronizer synchronizer,
        ILogger<AlarmRecurringJobSyncHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(synchronizer);
        ArgumentNullException.ThrowIfNull(logger);

        _synchronizer = synchronizer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _synchronizer.SyncAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync alarm recurring jobs from database at startup");
        }
    }
}
