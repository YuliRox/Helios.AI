namespace LumiRise.Api.Services.Alarm.Models;

/// <summary>
/// Represents the lifecycle state of an alarm.
/// </summary>
public enum AlarmState
{
    /// <summary>
    /// Alarm is scheduled but not currently executing.
    /// </summary>
    Idle,

    /// <summary>
    /// Scheduler has triggered the alarm; awaiting start of execution.
    /// </summary>
    Triggered,

    /// <summary>
    /// Alarm is actively executing a brightness ramp sequence.
    /// </summary>
    Running,

    /// <summary>
    /// Alarm scheduling is paused by user interaction.
    /// </summary>
    Paused,

    /// <summary>
    /// Alarm execution completed successfully (reached target brightness).
    /// </summary>
    Completed,

    /// <summary>
    /// Alarm execution was interrupted by manual override of the dimmer.
    /// </summary>
    Interrupted,

    /// <summary>
    /// Alarm execution failed due to an error (e.g., MQTT connection loss).
    /// </summary>
    Failed
}
