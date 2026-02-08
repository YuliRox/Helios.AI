namespace LumiRise.Api.Services.Alarm.Models;

/// <summary>
/// Events that cause state transitions in the alarm state machine.
/// </summary>
public enum AlarmTrigger
{
    /// <summary>
    /// Scheduler fires at the configured alarm time.
    /// Idle → Triggered
    /// </summary>
    SchedulerTrigger,

    /// <summary>
    /// Alarm execution begins (power on, start ramp).
    /// Triggered → Running
    /// </summary>
    Start,

    /// <summary>
    /// User cancels a triggered alarm before it starts running.
    /// Triggered → Idle
    /// </summary>
    Cancel,

    /// <summary>
    /// Manual override detected on the dimmer device.
    /// Running → Interrupted
    /// </summary>
    ManualOverride,

    /// <summary>
    /// Brightness ramp completed successfully.
    /// Running → Completed
    /// </summary>
    Complete,

    /// <summary>
    /// An error occurred during execution.
    /// Running → Failed
    /// </summary>
    Error,

    /// <summary>
    /// Reset alarm back to idle after completion, interruption, or failure.
    /// Completed/Interrupted/Failed → Idle
    /// </summary>
    Reset,

    /// <summary>
    /// User pauses alarm scheduling.
    /// Idle → Paused
    /// </summary>
    Pause,

    /// <summary>
    /// User resumes alarm scheduling from paused state.
    /// Paused → Idle
    /// </summary>
    Resume
}
