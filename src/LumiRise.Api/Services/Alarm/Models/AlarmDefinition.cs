namespace LumiRise.Api.Services.Alarm.Models;

/// <summary>
/// Defines an alarm's configuration and schedule.
/// </summary>
public class AlarmDefinition
{
    /// <summary>
    /// Unique identifier for this alarm.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the alarm.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this alarm is enabled and should be scheduled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time of day when the alarm triggers (HH:MM).
    /// </summary>
    public TimeOnly TriggerTime { get; set; }

    /// <summary>
    /// Days of the week when this alarm is active.
    /// </summary>
    public DayOfWeek[] DaysOfWeek { get; set; } = [];

    /// <summary>
    /// Starting brightness percentage for the ramp (default: 20%).
    /// </summary>
    public int StartBrightnessPercent { get; set; } = 20;

    /// <summary>
    /// Target brightness percentage at end of ramp (default: 100%).
    /// </summary>
    public int TargetBrightnessPercent { get; set; } = 100;

    /// <summary>
    /// Duration of the brightness ramp (default: 30 minutes).
    /// </summary>
    public TimeSpan RampDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Timezone for evaluating alarm trigger time.
    /// </summary>
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

    /// <summary>
    /// When this alarm definition was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this alarm definition was last modified.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
