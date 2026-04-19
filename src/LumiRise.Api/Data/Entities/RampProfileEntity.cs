namespace LumiRise.Api.Data.Entities;

/// <summary>
/// Persistent ramp configuration referenced by alarms.
/// </summary>
public class RampProfileEntity
{
    public const string DefaultMode = "default";
    public const int DefaultStartBrightnessPercent = 20;
    public const int DefaultTargetBrightnessPercent = 100;
    public const int DefaultRampDurationSeconds = 1800;
    public const int DefaultFullBrightnessDurationSeconds = 900;

    private int _startBrightnessPercent = DefaultStartBrightnessPercent;
    private int _targetBrightnessPercent = DefaultTargetBrightnessPercent;
    private int _rampDurationSeconds = DefaultRampDurationSeconds;
    private int _fullBrightnessDurationSeconds = DefaultFullBrightnessDurationSeconds;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Mode { get; set; } = DefaultMode;

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

    public int FullBrightnessDurationSeconds
    {
        get => _fullBrightnessDurationSeconds;
        set => _fullBrightnessDurationSeconds = Math.Max(0, value);
    }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AlarmScheduleEntity> AlarmSchedules { get; set; } = [];
}
