namespace LumiRise.Api.Configuration;

public class AlarmSchedulerOptions
{
    public const string SectionName = "AlarmScheduler";

    /// <summary>
    /// Interval in seconds for syncing recurring Hangfire jobs from database definitions.
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 30;
}
