using LumiRise.Api.Extensions;

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

    private int _startBrightnessPercent = 20;
    private int _targetBrightnessPercent = 100;

    /// <summary>
    /// Starting brightness percentage for the ramp (default: 20%).
    /// Clamped to 0–100.
    /// </summary>
    public int StartBrightnessPercent
    {
        get => _startBrightnessPercent;
        set => _startBrightnessPercent = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Target brightness percentage at end of ramp (default: 100%).
    /// Clamped to 0–100.
    /// </summary>
    public int TargetBrightnessPercent
    {
        get => _targetBrightnessPercent;
        set => _targetBrightnessPercent = Math.Clamp(value, 0, 100);
    }

    private static readonly TimeSpan MinRampDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRampDuration = TimeSpan.FromDays(1);
    private TimeSpan _rampDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Duration of the brightness ramp (default: 30 minutes).
    /// Clamped to 1 second – 1 day.
    /// </summary>
    public TimeSpan RampDuration
    {
        get => _rampDuration;
        set => _rampDuration = value.Clamp(MinRampDuration, MaxRampDuration);
    }

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
