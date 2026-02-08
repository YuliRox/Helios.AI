using LumiRise.Api.Services.Alarm.Models;

namespace LumiRise.Api.Services.Alarm.Interfaces;

/// <summary>
/// Manages the state machine for a single alarm instance.
/// Handles state transitions, validates transition legality,
/// and orchestrates dimmer commands during alarm execution.
/// </summary>
public interface IAlarmStateMachine
{
    /// <summary>
    /// The alarm definition this state machine manages.
    /// </summary>
    AlarmDefinition Definition { get; }

    /// <summary>
    /// The current state of this alarm.
    /// </summary>
    AlarmState CurrentState { get; }

    /// <summary>
    /// Observable stream of state transitions for this alarm.
    /// </summary>
    IObservable<AlarmStateTransition> StateTransitions { get; }

    /// <summary>
    /// Fires a trigger to transition the alarm state.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <param name="message">Optional context message for the transition.</param>
    /// <returns>The resulting state after the transition.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the trigger is not valid for the current state.
    /// </exception>
    AlarmState Fire(AlarmTrigger trigger, string? message = null);

    /// <summary>
    /// Checks whether a trigger can be fired in the current state.
    /// </summary>
    /// <param name="trigger">The trigger to check.</param>
    /// <returns>True if the trigger is valid for the current state.</returns>
    bool CanFire(AlarmTrigger trigger);

    /// <summary>
    /// Gets all triggers that are valid for the current state.
    /// </summary>
    /// <returns>Collection of valid triggers.</returns>
    IReadOnlyCollection<AlarmTrigger> GetPermittedTriggers();

    /// <summary>
    /// Executes the alarm's brightness ramp sequence.
    /// Should be called after transitioning to the Running state.
    /// Handles power-on, brightness ramping, and interruption detection.
    /// </summary>
    /// <param name="ct">Cancellation token to abort execution.</param>
    /// <returns>A task that completes when the ramp finishes, is interrupted, or fails.</returns>
    Task ExecuteAsync(CancellationToken ct = default);
}
