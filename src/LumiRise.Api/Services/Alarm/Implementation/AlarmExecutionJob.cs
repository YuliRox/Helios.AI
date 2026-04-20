using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using LumiRise.Api.Configuration;
using LumiRise.Api.Data;
using LumiRise.Api.Services.Alarm.Interfaces;
using LumiRise.Api.Services.Alarm.Models;
using LumiRise.Api.Services.Mqtt.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LumiRise.Api.Services.Alarm.Implementation;

public sealed class AlarmExecutionJob
{
    private static readonly SemaphoreSlim ExecutionMutex = new(1, 1);

    private readonly LumiRiseDbContext _dbContext;
    private readonly IAlarmStateMachineFactory _stateMachineFactory;
    private readonly IMqttConnectionManager _mqttConnectionManager;
    private readonly IDimmerStateMonitor _stateMonitor;
    private readonly AlarmSettingsOptions _alarmSettings;
    private readonly ILogger<AlarmExecutionJob> _logger;

    public AlarmExecutionJob(
        LumiRiseDbContext dbContext,
        IAlarmStateMachineFactory stateMachineFactory,
        IMqttConnectionManager mqttConnectionManager,
        IDimmerStateMonitor stateMonitor,
        IOptions<AlarmSettingsOptions> alarmSettingsOptions,
        ILogger<AlarmExecutionJob> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(stateMachineFactory);
        ArgumentNullException.ThrowIfNull(mqttConnectionManager);
        ArgumentNullException.ThrowIfNull(stateMonitor);
        ArgumentNullException.ThrowIfNull(alarmSettingsOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _stateMachineFactory = stateMachineFactory;
        _mqttConnectionManager = mqttConnectionManager;
        _stateMonitor = stateMonitor;
        _alarmSettings = alarmSettingsOptions.Value;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid alarmId, PerformContext? context = null)
    {
        await ExecutionMutex.WaitAsync(CancellationToken.None);

        try
        {
            var alarm = await _dbContext.AlarmSchedules
                .AsNoTracking()
                .Include(x => x.RampProfile)
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

            if (alarm.RampProfile is null)
            {
                _logger.LogWarning("Skipping alarm execution for {AlarmId}: ramp profile not found", alarmId);
                return;
            }

            var definition = new AlarmDefinition
            {
                Id = alarm.Id,
                Name = alarm.Name,
                Enabled = alarm.Enabled,
                StartBrightnessPercent = alarm.RampProfile.StartBrightnessPercent,
                TargetBrightnessPercent = alarm.RampProfile.TargetBrightnessPercent,
                RampDuration = TimeSpan.FromSeconds(alarm.RampProfile.RampDurationSeconds),
                FullBrightnessDuration = TimeSpan.FromSeconds(alarm.RampProfile.FullBrightnessDurationSeconds),
                TimeZoneId = _alarmSettings.TimeZoneId,
                UpdatedAt = alarm.UpdatedAtUtc,
                CreatedAt = alarm.CreatedAtUtc
            };

            _logger.LogInformation(
                "Starting Hangfire alarm job for {AlarmId} ({AlarmName}) with ramp {Start}% -> {Target}% in {RampDuration}, hold {HoldDuration}",
                alarm.Id,
                alarm.Name,
                definition.StartBrightnessPercent,
                definition.TargetBrightnessPercent,
                definition.RampDuration,
                definition.FullBrightnessDuration);
            WriteInfo(
                context,
                "Starting alarm '{0}' ({1}): {2}% -> {3}% in {4}, hold {5}.",
                alarm.Name,
                alarm.Id,
                definition.StartBrightnessPercent,
                definition.TargetBrightnessPercent,
                definition.RampDuration,
                definition.FullBrightnessDuration);

            try
            {
                await _mqttConnectionManager.ConnectAsync(CancellationToken.None);
                await _stateMonitor.StartMonitoringAsync(CancellationToken.None);
                WriteInfo(context, "Connected to MQTT and started state monitoring.");

                var machine = _stateMachineFactory.Create(definition);
                var machineDisposable = machine as IDisposable;
                void OnBrightnessChanged(int brightnessPercent) =>
                    WriteInfo(context, "Set brightness to {0}%", brightnessPercent);

                machine.BrightnessChanged += OnBrightnessChanged;

                try
                {
                    machine.Fire(AlarmTrigger.SchedulerTrigger, "Triggered by Hangfire recurring job");
                    machine.Fire(AlarmTrigger.Start, "Starting alarm execution");
                    await machine.ExecuteAsync(CancellationToken.None);

                    if (machine.CurrentState == AlarmState.Completed)
                    {
                        WriteSuccess(context, "Alarm completed successfully.");
                    }
                    else if (machine.CurrentState == AlarmState.Interrupted)
                    {
                        WriteInfo(context, "Alarm interrupted by manual override.");
                    }
                }
                finally
                {
                    machine.BrightnessChanged -= OnBrightnessChanged;
                    machineDisposable?.Dispose();
                }
            }
            finally
            {
                try
                {
                    WriteInfo(context, "Stopping dimmer state monitoring...");
                    await _stateMonitor.StopMonitoringAsync(CancellationToken.None);
                    WriteInfo(context, "Dimmer state monitoring stopped.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to stop dimmer monitoring after alarm job {AlarmId}", alarmId);
                    WriteError(context, "Cleanup error while stopping state monitoring: {0}", ex.Message);
                }

                try
                {
                    WriteInfo(context, "Disconnecting MQTT...");
                    await _mqttConnectionManager.DisconnectAsync(CancellationToken.None);
                    WriteInfo(context, "MQTT disconnected.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to disconnect MQTT after alarm job {AlarmId}", alarmId);
                    WriteError(context, "Cleanup error while disconnecting MQTT: {0}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            WriteError(context, "Alarm failed: {0}", ex.Message);
            throw;
        }
        finally
        {
            ExecutionMutex.Release();
        }
    }

    private static void WriteInfo(PerformContext? context, string message, params object[] args)
    {
        if (context is null)
        {
            return;
        }

        context.WriteLine(message, args);
    }

    private static void WriteSuccess(PerformContext? context, string message, params object[] args)
    {
        if (context is null)
        {
            return;
        }

        context.SetTextColor(ConsoleTextColor.Green);
        context.WriteLine(message, args);
        context.ResetTextColor();
    }

    private static void WriteError(PerformContext? context, string message, params object[] args)
    {
        if (context is null)
        {
            return;
        }

        context.SetTextColor(ConsoleTextColor.Red);
        context.WriteLine(message, args);
        context.ResetTextColor();
    }
}
