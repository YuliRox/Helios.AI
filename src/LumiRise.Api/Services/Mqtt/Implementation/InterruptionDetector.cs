using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace LumiRise.Api.Services.Mqtt.Implementation;

/// <summary>
/// Detects manual interruptions to alarm execution.
/// Compares expected dimmer state with actual state to identify user intervention.
/// </summary>
public class InterruptionDetector : IInterruptionDetector
{
    private readonly ILogger<InterruptionDetector> _logger;
    private readonly IDimmerStateMonitor _stateMonitor;
    private readonly Subject<InterruptionEvent> _interruptionsSubject = new();
    private readonly object _stateLock = new();

    private DimmerState? _expectedState;
    private bool _detectionEnabled;
    private IDisposable? _stateSubscription;

    public InterruptionDetector(
        ILogger<InterruptionDetector> logger,
        IDimmerStateMonitor stateMonitor)
    {
        _logger = logger;
        _stateMonitor = stateMonitor;

        // Subscribe to state changes to detect interruptions
        _stateSubscription = _stateMonitor.StateChanges
            .Subscribe(actualState => CheckForInterruption(actualState));
    }

    public IObservable<InterruptionEvent> Interruptions => _interruptionsSubject.AsObservable();

    public void SetExpectedState(DimmerState expected)
    {
        lock (_stateLock)
        {
            _expectedState = expected;
            _logger.LogDebug("Expected dimmer state set: {State}", expected);
        }
    }

    public void ClearExpectedState()
    {
        lock (_stateLock)
        {
            _expectedState = null;
            _logger.LogDebug("Expected dimmer state cleared");
        }
    }

    public void EnableDetection()
    {
        lock (_stateLock)
        {
            _detectionEnabled = true;
            _logger.LogDebug("Interruption detection enabled");
        }
    }

    public void DisableDetection()
    {
        lock (_stateLock)
        {
            _detectionEnabled = false;
            _logger.LogDebug("Interruption detection disabled");
        }
    }

    private void CheckForInterruption(DimmerState actualState)
    {
        lock (_stateLock)
        {
            // Only check if detection is enabled and we have an expected state
            if (!_detectionEnabled || _expectedState == null)
                return;

            // Detect power-off interruption
            if (_expectedState.IsOn && !actualState.IsOn)
            {
                PublishInterruption(
                    InterruptionReason.ManualPowerOff,
                    "User manually turned off the dimmer",
                    actualState);
                return;
            }

            // Detect brightness adjustment interruption
            // Allow small differences (tolerance of 2%) to account for rounding errors
            const int brightnessTolerance = 2;

            if (_expectedState.IsOn && actualState.IsOn &&
                Math.Abs(_expectedState.BrightnessPercent - actualState.BrightnessPercent) > brightnessTolerance)
            {
                PublishInterruption(
                    InterruptionReason.ManualBrightnessAdjustment,
                    $"User adjusted brightness from {_expectedState.BrightnessPercent}% to {actualState.BrightnessPercent}%",
                    actualState);
                return;
            }

            // Detect power-on interruption when we expected off
            if (!_expectedState.IsOn && actualState.IsOn)
            {
                PublishInterruption(
                    InterruptionReason.ManualPowerOn,
                    "User manually turned on the dimmer",
                    actualState);
            }
        }
    }

    private void PublishInterruption(InterruptionReason reason, string message, DimmerState actualState)
    {
        var @event = new InterruptionEvent
        {
            Reason = reason,
            ExpectedState = _expectedState,
            ActualState = actualState,
            Message = message
        };

        _logger.LogWarning("Alarm interrupted: {Event}", @event);
        _interruptionsSubject.OnNext(@event);
    }

    public void Dispose()
    {
        _stateSubscription?.Dispose();
        _interruptionsSubject?.Dispose();
        GC.SuppressFinalize(this);
    }
}
