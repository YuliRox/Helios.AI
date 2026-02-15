namespace LumiRise.Api.Data.Entities;

/// <summary>
/// Persistent alarm schedule definition used to create/update Hangfire recurring jobs.
/// </summary>
public class AlarmScheduleEntity
{
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

    public Guid RampProfileId { get; set; }

    public RampProfileEntity RampProfile { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
