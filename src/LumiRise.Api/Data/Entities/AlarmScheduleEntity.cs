namespace LumiRise.Api.Data.Entities;

/// <summary>
/// Persistent alarm schedule definition used to create/update Hangfire recurring jobs.
/// </summary>
public class AlarmScheduleEntity
{
    private int _startBrightnessPercent = 20;
    private int _targetBrightnessPercent = 100;
    private int _rampDurationSeconds = 1800;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hangfire/NCrontab expression (5-part format).
    /// </summary>
    public string CronExpression { get; set; } = "0 7 * * *";

    /// <summary>
    /// IANA/Windows timezone id used by Hangfire for schedule evaluation.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    public int StartBrightnessPercent
    {
        get => _startBrightnessPercent;
        set => _startBrightnessPercent = Math.Clamp(value, 0, 100);
    }

    public int TargetBrightnessPercent
    {
        get => _targetBrightnessPercent;
        set => _targetBrightnessPercent = Math.Clamp(value, 0, 100);
    }

    public int RampDurationSeconds
    {
        get => _rampDurationSeconds;
        set => _rampDurationSeconds = Math.Max(1, value);
    }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
