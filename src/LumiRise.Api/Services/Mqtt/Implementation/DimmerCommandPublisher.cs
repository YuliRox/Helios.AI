using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LumiRise.Api.Services.Mqtt.Implementation;

/// <summary>
/// Publishes commands to control the dimmer device.
/// Handles power control, brightness setting, and brightness ramping with minimum threshold enforcement.
/// </summary>
public class DimmerCommandPublisher : IDimmerCommandPublisher
{
    private readonly ILogger<DimmerCommandPublisher> _logger;
    private readonly IMqttConnectionManager _connectionManager;
    private readonly MqttOptions _options;
    private readonly SemaphoreSlim _commandMutex = new(1, 1);

    public DimmerCommandPublisher(
        ILogger<DimmerCommandPublisher> logger,
        IMqttConnectionManager connectionManager,
        IOptions<MqttOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _connectionManager = connectionManager;
        _options = options.Value;
    }

    public async Task TurnOnAsync(CancellationToken ct)
    {
        await _commandMutex.WaitAsync(ct);
        try
        {
            await PublishPowerCommandAsync(true, ct);
        }
        finally
        {
            _commandMutex.Release();
        }
    }

    public async Task TurnOffAsync(CancellationToken ct)
    {
        await _commandMutex.WaitAsync(ct);
        try
        {
            await PublishPowerCommandAsync(false, ct);
        }
        finally
        {
            _commandMutex.Release();
        }
    }

    public async Task SetBrightnessAsync(int percentage, CancellationToken ct)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentException("Brightness percentage must be between 0 and 100", nameof(percentage));

        await _commandMutex.WaitAsync(ct);
        try
        {
            // Apply minimum brightness threshold
            var effectivePercentage = ApplyMinimumBrightnessThreshold(percentage);

            if (effectivePercentage == 0)
            {
                // Turn off if below minimum threshold
                await TurnOffAsync(ct);
            }
            else
            {
                // Publish brightness percentage as plain integer
                await _connectionManager.PublishAsync(
                    _options.Topics.DimmerPercentageCommand,
                    effectivePercentage.ToString(),
                    ct);
                _logger.LogInformation("Set brightness to {Percentage}%", effectivePercentage);
            }
        }
        finally
        {
            _commandMutex.Release();
        }
    }

    public async Task RampBrightnessAsync(int start, int target, TimeSpan duration,
        IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (start < 0 || start > 100)
            throw new ArgumentException("Start brightness must be between 0 and 100", nameof(start));

        if (target < 0 || target > 100)
            throw new ArgumentException("Target brightness must be between 0 and 100", nameof(target));

        try
        {
            var stepDelayMs = _options.RampStepDelayMs;
            var totalSteps = (int)Math.Ceiling(duration.TotalMilliseconds / stepDelayMs);

            if (totalSteps <= 0)
                totalSteps = 1;

            var step = 0;
            var currentBrightness = start;

            while (step < totalSteps && !ct.IsCancellationRequested)
            {
                // Calculate next brightness level
                var progress_ratio = (double)step / (totalSteps - 1);
                var nextBrightness = (int)Math.Round(start + (target - start) * progress_ratio);

                // Clamp to range [0, 100]
                nextBrightness = Math.Clamp(nextBrightness, 0, 100);

                // Only send command if brightness changed
                if (nextBrightness != currentBrightness)
                {
                    await SetBrightnessAsync(nextBrightness, ct);
                    currentBrightness = nextBrightness;
                    progress?.Report(nextBrightness);
                }

                step++;

                // Wait before next step (except after the last step)
                if (step < totalSteps)
                {
                    await Task.Delay(stepDelayMs, ct);
                }
            }

            // Ensure we end at the target
            if (currentBrightness != target && !ct.IsCancellationRequested)
            {
                await SetBrightnessAsync(target, ct);
                progress?.Report(target);
            }

            _logger.LogInformation("Brightness ramp complete: {Start}% -> {Target}% over {Duration}",
                start, target, duration);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Brightness ramp cancelled");
            throw;
        }
    }

    /// <summary>
    /// Publishes a power on/off command. Must be called while holding _commandMutex.
    /// </summary>
    private async Task PublishPowerCommandAsync(bool on, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { POWER = on ? "ON" : "OFF" });
        await _connectionManager.PublishAsync(
            _options.Topics.DimmerOnOffCommand,
            payload,
            ct);
        _logger.LogInformation("Turned dimmer {State}", on ? "on" : "off");
    }

    private int ApplyMinimumBrightnessThreshold(int percentage)
    {
        // If percentage is below minimum threshold, return 0 (off)
        // Otherwise, return the percentage as-is
        return percentage < _options.MinimumBrightnessPercent ? 0 : percentage;
    }
}
