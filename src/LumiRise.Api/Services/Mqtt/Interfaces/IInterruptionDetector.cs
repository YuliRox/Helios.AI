using LumiRise.Api.Services.Mqtt.Models;

namespace LumiRise.Api.Services.Mqtt.Interfaces;

/// <summary>
/// Detects manual interruptions during alarm execution.
/// Compares expected dimmer state with actual state to identify user intervention.
/// </summary>
public interface IInterruptionDetector
{
    /// <summary>
    /// Gets an observable stream of interruption events.
    /// </summary>
    IObservable<InterruptionEvent> Interruptions { get; }

    /// <summary>
    /// Sets the expected dimmer state for comparison.
    /// Used to detect when actual state diverges from expectations.
    /// </summary>
    /// <param name="expected">The expected state during alarm execution.</param>
    void SetExpectedState(DimmerState expected);

    /// <summary>
    /// Clears the expected state.
    /// Stops interruption detection until a new expected state is set.
    /// </summary>
    void ClearExpectedState();

    /// <summary>
    /// Enables interruption detection.
    /// Detection will only trigger if an expected state is also set.
    /// </summary>
    void EnableDetection();

    /// <summary>
    /// Disables interruption detection.
    /// No interruption events will be published until re-enabled.
    /// </summary>
    void DisableDetection();
}
