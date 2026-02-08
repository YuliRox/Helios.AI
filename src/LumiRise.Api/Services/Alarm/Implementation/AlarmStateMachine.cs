using System.Reactive.Linq;
using System.Reactive.Subjects;
using LumiRise.Api.Services.Alarm.Interfaces;
using LumiRise.Api.Services.Alarm.Models;
using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Logging;

namespace LumiRise.Api.Services.Alarm.Implementation;

/// <summary>
/// State machine implementation for a single alarm.
/// Manages state transitions, validates transition legality,
/// and orchestrates dimmer commands during alarm execution.
/// </summary>
public class AlarmStateMachine : IAlarmStateMachine, IDisposable
{
    private readonly IDimmerCommandPublisher _commandPublisher;
    private readonly IInterruptionDetector _interruptionDetector;
    private readonly ILogger<AlarmStateMachine> _logger;
    private readonly Subject<AlarmStateTransition> _stateTransitions = new();
    private readonly object _stateLock = new();

    private static readonly Dictionary<(AlarmState State, AlarmTrigger Trigger), AlarmState> TransitionTable = new()
    {
        { (AlarmState.Idle, AlarmTrigger.SchedulerTrigger), AlarmState.Triggered },
        { (AlarmState.Idle, AlarmTrigger.Pause), AlarmState.Paused },
        { (AlarmState.Triggered, AlarmTrigger.Start), AlarmState.Running },
        { (AlarmState.Triggered, AlarmTrigger.Cancel), AlarmState.Idle },
        { (AlarmState.Running, AlarmTrigger.ManualOverride), AlarmState.Interrupted },
        { (AlarmState.Running, AlarmTrigger.Complete), AlarmState.Completed },
        { (AlarmState.Running, AlarmTrigger.Error), AlarmState.Failed },
        { (AlarmState.Interrupted, AlarmTrigger.Reset), AlarmState.Idle },
        { (AlarmState.Completed, AlarmTrigger.Reset), AlarmState.Idle },
        { (AlarmState.Failed, AlarmTrigger.Reset), AlarmState.Idle },
        { (AlarmState.Paused, AlarmTrigger.Resume), AlarmState.Idle },
    };

    public AlarmStateMachine(
        AlarmDefinition definition,
        IDimmerCommandPublisher commandPublisher,
        IInterruptionDetector interruptionDetector,
        ILogger<AlarmStateMachine> logger)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _commandPublisher = commandPublisher ?? throw new ArgumentNullException(nameof(commandPublisher));
        _interruptionDetector = interruptionDetector ?? throw new ArgumentNullException(nameof(interruptionDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AlarmDefinition Definition { get; }

    public AlarmState CurrentState { get; private set; } = AlarmState.Idle;

    public IObservable<AlarmStateTransition> StateTransitions => _stateTransitions.AsObservable();

    public AlarmState Fire(AlarmTrigger trigger, string? message = null)
    {
        lock (_stateLock)
        {
            var key = (CurrentState, trigger);
            if (!TransitionTable.TryGetValue(key, out var newState))
            {
                throw new InvalidOperationException(
                    $"Cannot fire trigger '{trigger}' from state '{CurrentState}' " +
                    $"for alarm '{Definition.Name}' ({Definition.Id}).");
            }

            var previousState = CurrentState;
            CurrentState = newState;

            var transition = new AlarmStateTransition
            {
                AlarmId = Definition.Id,
                PreviousState = previousState,
                NewState = newState,
                Trigger = trigger,
                Message = message
            };

            _logger.LogInformation("Alarm state transition: {Transition}", transition);
            _stateTransitions.OnNext(transition);

            return newState;
        }
    }

    public bool CanFire(AlarmTrigger trigger)
    {
        lock (_stateLock)
        {
            return TransitionTable.ContainsKey((CurrentState, trigger));
        }
    }

    public IReadOnlyCollection<AlarmTrigger> GetPermittedTriggers()
    {
        lock (_stateLock)
        {
            return TransitionTable.Keys
                .Where(k => k.State == CurrentState)
                .Select(k => k.Trigger)
                .ToArray();
        }
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (CurrentState != AlarmState.Running)
        {
            throw new InvalidOperationException(
                $"Cannot execute alarm '{Definition.Name}' in state '{CurrentState}'. " +
                "Alarm must be in Running state.");
        }

        _logger.LogInformation(
            "Starting alarm execution for '{AlarmName}' ({AlarmId}): " +
            "ramping {Start}% → {Target}% over {Duration}",
            Definition.Name, Definition.Id,
            Definition.StartBrightnessPercent,
            Definition.TargetBrightnessPercent,
            Definition.RampDuration);

        using var interruptionSubscription = _interruptionDetector.Interruptions
            .Subscribe(OnInterruptionDetected);

        try
        {
            // Enable interruption detection during execution
            _interruptionDetector.SetExpectedState(new DimmerState
            {
                IsOn = true,
                BrightnessPercent = Definition.StartBrightnessPercent
            });
            _interruptionDetector.EnableDetection();

            // Turn on the dimmer
            await _commandPublisher.TurnOnAsync(ct);

            // Set initial brightness
            await _commandPublisher.SetBrightnessAsync(Definition.StartBrightnessPercent, ct);

            // Execute brightness ramp with progress tracking
            var progress = new Progress<int>(currentBrightness =>
            {
                _interruptionDetector.SetExpectedState(new DimmerState
                {
                    IsOn = true,
                    BrightnessPercent = currentBrightness
                });

                _logger.LogDebug(
                    "Alarm '{AlarmName}' ramp progress: {Brightness}%",
                    Definition.Name, currentBrightness);
            });

            await _commandPublisher.RampBrightnessAsync(
                Definition.StartBrightnessPercent,
                Definition.TargetBrightnessPercent,
                Definition.RampDuration,
                progress,
                ct);

            // Ramp completed successfully
            if (CurrentState == AlarmState.Running)
            {
                Fire(AlarmTrigger.Complete, "Brightness ramp completed successfully");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Alarm '{AlarmName}' execution cancelled", Definition.Name);

            if (CurrentState == AlarmState.Running)
            {
                Fire(AlarmTrigger.Error, "Execution cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Alarm '{AlarmName}' execution failed: {Error}",
                Definition.Name, ex.Message);

            if (CurrentState == AlarmState.Running)
            {
                Fire(AlarmTrigger.Error, ex.Message);
            }
        }
        finally
        {
            _interruptionDetector.DisableDetection();
            _interruptionDetector.ClearExpectedState();
        }
    }

    private void OnInterruptionDetected(InterruptionEvent interruption)
    {
        _logger.LogWarning(
            "Alarm '{AlarmName}' interrupted: {Reason} - {Message}",
            Definition.Name, interruption.Reason, interruption.Message);

        if (CanFire(AlarmTrigger.ManualOverride))
        {
            Fire(AlarmTrigger.ManualOverride,
                $"{interruption.Reason}: {interruption.Message}");
        }
    }

    public void Dispose()
    {
        _stateTransitions.Dispose();
        GC.SuppressFinalize(this);
    }
}
