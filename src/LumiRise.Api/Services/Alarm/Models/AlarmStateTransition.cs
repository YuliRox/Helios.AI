namespace LumiRise.Api.Services.Alarm.Models;

/// <summary>
/// Records a state transition in the alarm state machine.
/// </summary>
public class AlarmStateTransition
{
    /// <summary>
    /// The alarm this transition belongs to.
    /// </summary>
    public Guid AlarmId { get; init; }

    /// <summary>
    /// The state before the transition.
    /// </summary>
    public AlarmState PreviousState { get; init; }

    /// <summary>
    /// The state after the transition.
    /// </summary>
    public AlarmState NewState { get; init; }

    /// <summary>
    /// The trigger that caused this transition.
    /// </summary>
    public AlarmTrigger Trigger { get; init; }

    /// <summary>
    /// When the transition occurred.
    /// </summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional message providing context for the transition.
    /// </summary>
    public string? Message { get; init; }

    public override string ToString() =>
        $"[{AlarmId.ToString("N")[..8]}] {PreviousState} --[{Trigger}]--> {NewState}" +
        (Message is not null ? $" ({Message})" : "");
}
