using Hangfire;
using LumiRise.Api.Data;
using LumiRise.Api.Services.Alarm.Interfaces;
using LumiRise.Api.Services.Alarm.Models;
using LumiRise.Api.Services.Mqtt.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LumiRise.Api.Services.Alarm.Implementation;

public sealed class AlarmExecutionJob
{
    private readonly LumiRiseDbContext _dbContext;
    private readonly IAlarmStateMachineFactory _stateMachineFactory;
    private readonly IMqttConnectionManager _mqttConnectionManager;
    private readonly IDimmerStateMonitor _stateMonitor;
    private readonly ILogger<AlarmExecutionJob> _logger;

    public AlarmExecutionJob(
        LumiRiseDbContext dbContext,
        IAlarmStateMachineFactory stateMachineFactory,
        IMqttConnectionManager mqttConnectionManager,
        IDimmerStateMonitor stateMonitor,
        ILogger<AlarmExecutionJob> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(stateMachineFactory);
        ArgumentNullException.ThrowIfNull(mqttConnectionManager);
        ArgumentNullException.ThrowIfNull(stateMonitor);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _stateMachineFactory = stateMachineFactory;
        _mqttConnectionManager = mqttConnectionManager;
        _stateMonitor = stateMonitor;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task ExecuteAsync(Guid alarmId)
    {
        var alarm = await _dbContext.AlarmSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == alarmId);

        if (alarm is null)
        {
            _logger.LogWarning("Skipping alarm execution for {AlarmId}: alarm not found", alarmId);
            return;
        }

        if (!alarm.Enabled)
        {
            _logger.LogInformation("Skipping alarm execution for {AlarmId}: alarm is disabled", alarmId);
            return;
        }

        var definition = new AlarmDefinition
        {
            Id = alarm.Id,
            Name = alarm.Name,
            Enabled = alarm.Enabled,
            StartBrightnessPercent = alarm.StartBrightnessPercent,
            TargetBrightnessPercent = alarm.TargetBrightnessPercent,
            RampDuration = TimeSpan.FromSeconds(Math.Max(1, alarm.RampDurationSeconds)),
            TimeZoneId = alarm.TimeZoneId,
            UpdatedAt = alarm.UpdatedAtUtc,
            CreatedAt = alarm.CreatedAtUtc
        };

        _logger.LogInformation("Starting Hangfire alarm job for {AlarmId} ({AlarmName})", alarm.Id, alarm.Name);

        try
        {
            await _mqttConnectionManager.ConnectAsync(CancellationToken.None);
            await _stateMonitor.StartMonitoringAsync(CancellationToken.None);

            var machine = _stateMachineFactory.Create(definition);
            using var _ = machine as IDisposable;

            machine.Fire(AlarmTrigger.SchedulerTrigger, "Triggered by Hangfire recurring job");
            machine.Fire(AlarmTrigger.Start, "Starting alarm execution");
            await machine.ExecuteAsync(CancellationToken.None);
        }
        finally
        {
            try
            {
                await _stateMonitor.StopMonitoringAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop dimmer monitoring after alarm job {AlarmId}", alarm.Id);
            }

            try
            {
                await _mqttConnectionManager.DisconnectAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to disconnect MQTT after alarm job {AlarmId}", alarm.Id);
            }
        }
    }
}
