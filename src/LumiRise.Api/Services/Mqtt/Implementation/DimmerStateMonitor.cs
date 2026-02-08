using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace LumiRise.Api.Services.Mqtt.Implementation;

/// <summary>
/// Monitors dimmer device state by parsing messages from status topics.
/// Maintains cached state and publishes updates via observable.
/// </summary>
public class DimmerStateMonitor : IDimmerStateMonitor
{
    private readonly ILogger<DimmerStateMonitor> _logger;
    private readonly IMqttConnectionManager _connectionManager;
    private readonly MqttOptions _options;
    private readonly Subject<DimmerState> _stateChangesSubject = new();
    private readonly object _stateLock = new();
    private readonly CancellationTokenSource _disposalCts = new();

    private DimmerState? _currentState;
    private IDisposable? _messageSubscription;
    private bool _disposed;

    public DimmerStateMonitor(
        ILogger<DimmerStateMonitor> logger,
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

    public DimmerState? CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    public IObservable<DimmerState> StateChanges => _stateChangesSubject.AsObservable();

    public async Task StartMonitoringAsync(CancellationToken ct)
    {
        try
        {
            // Subscribe to both power and result status topics
            await _connectionManager.SubscribeAsync(_options.Topics.DimmerOnOffStatus, ct);
            await _connectionManager.SubscribeAsync(_options.Topics.DimmerPercentageStatus, ct);

            _logger.LogInformation("Started monitoring dimmer state");

            // Subscribe to message received events
            _messageSubscription = _connectionManager.MessageReceived
                .Subscribe(msg => ProcessMessage(msg.Topic, msg.Payload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting state monitoring");
            throw;
        }
    }

    public Task StopMonitoringAsync(CancellationToken ct)
    {
        _messageSubscription?.Dispose();
        _logger.LogInformation("Stopped monitoring dimmer state");
        return Task.CompletedTask;
    }

    private void ProcessMessage(string topic, string payload)
    {
        try
        {
            DimmerState? newState = null;

            // Parse power status from DimmerOnOffStatus topic (plain text "ON"/"OFF")
            if (topic == _options.Topics.DimmerOnOffStatus)
            {
                newState = ParsePowerMessage(payload);
            }
            // Parse brightness from DimmerPercentageStatus topic (JSON with Dimmer and POWER fields)
            else if (topic == _options.Topics.DimmerPercentageStatus)
            {
                newState = ParseResultMessage(payload);
            }

            if (newState != null)
            {
                UpdateState(newState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing message from {Topic}: {Payload}",
                topic, payload);
        }
    }

    private DimmerState? ParsePowerMessage(string payload)
    {
        // Plain text power status: "ON" or "OFF"
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        var isOn = payload.Trim().Equals("ON", StringComparison.OrdinalIgnoreCase);

        lock (_stateLock)
        {
            // If we have a current state, update only the power bit
            if (_currentState != null)
            {
                return new DimmerState
                {
                    IsOn = isOn,
                    BrightnessPercent = isOn ? _currentState.BrightnessPercent : 0
                };
            }

            // Otherwise create a new state
            return new DimmerState
            {
                IsOn = isOn,
                BrightnessPercent = isOn ? 50 : 0 // Default to 50% if turning on without prior state
            };
        }
    }

    private DimmerState? ParseResultMessage(string payload)
    {
        // JSON result message: {"POWER":"ON","Dimmer":75}
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("POWER", out var powerElement) ||
                !root.TryGetProperty("Dimmer", out var dimmerElement))
            {
                _logger.LogDebug("Missing POWER or Dimmer field in JSON: {Payload}", payload);
                return null;
            }

            var isOn = powerElement.GetString()?.Equals("ON", StringComparison.OrdinalIgnoreCase) ?? false;
            var brightness = dimmerElement.GetInt32();

            return new DimmerState
            {
                IsOn = isOn,
                BrightnessPercent = brightness
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON result message: {Payload}", payload);
            return null;
        }
    }

    private void UpdateState(DimmerState newState)
    {
        lock (_stateLock)
        {
            // Only publish if state actually changed
            if (_currentState?.Equals(newState) == true)
            {
                return;
            }

            _currentState = newState;
            _logger.LogDebug("Dimmer state updated: {State}", newState);
            _stateChangesSubject.OnNext(newState);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _disposalCts.Cancel();
        _disposalCts.Dispose();

        _messageSubscription?.Dispose();
        _stateChangesSubject.OnCompleted();
        _stateChangesSubject.Dispose();

        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }
}
