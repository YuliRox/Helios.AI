using Hangfire;
using Hangfire.Storage;
using LumiRise.Api.Data;
using LumiRise.Api.Services.Alarm.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LumiRise.Api.Services.Alarm.Implementation;

public sealed class AlarmRecurringJobSynchronizer : IAlarmRecurringJobSynchronizer
{
    internal const string AlarmRecurringJobPrefix = "alarm:";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly JobStorage _jobStorage;
    private readonly ILogger<AlarmRecurringJobSynchronizer> _logger;

    public AlarmRecurringJobSynchronizer(
        IServiceScopeFactory scopeFactory,
        IRecurringJobManager recurringJobManager,
        JobStorage jobStorage,
        ILogger<AlarmRecurringJobSynchronizer> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(recurringJobManager);
        ArgumentNullException.ThrowIfNull(jobStorage);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _recurringJobManager = recurringJobManager;
        _jobStorage = jobStorage;
        _logger = logger;
    }

    public static string BuildRecurringJobId(Guid alarmId) => $"{AlarmRecurringJobPrefix}{alarmId:N}";

    public async Task SyncAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LumiRiseDbContext>();

        var alarms = await dbContext.AlarmSchedules
            .AsNoTracking()
            .ToListAsync(ct);

        var expectedRecurringIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var alarm in alarms.Where(a => a.Enabled))
        {
            var recurringJobId = BuildRecurringJobId(alarm.Id);
            expectedRecurringIds.Add(recurringJobId);

            var timeZone = ResolveTimeZone(alarm.TimeZoneId);
            try
            {
                _recurringJobManager.AddOrUpdate<AlarmExecutionJob>(
                    recurringJobId,
                    job => job.ExecuteAsync(alarm.Id),
                    alarm.CronExpression,
                    new RecurringJobOptions
                    {
                        TimeZone = timeZone
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to add or update recurring alarm {AlarmId} ({AlarmName})",
                    alarm.Id,
                    alarm.Name);
            }
        }

        using var connection = _jobStorage.GetConnection();
        var existingAlarmRecurringJobs = connection.GetRecurringJobs()
            .Where(x => x.Id.StartsWith(AlarmRecurringJobPrefix, StringComparison.Ordinal))
            .Select(x => x.Id)
            .ToArray();

        foreach (var recurringJobId in existingAlarmRecurringJobs)
        {
            if (expectedRecurringIds.Contains(recurringJobId))
            {
                continue;
            }

            _recurringJobManager.RemoveIfExists(recurringJobId);
            _logger.LogInformation("Removed stale recurring alarm job {RecurringJobId}", recurringJobId);
        }
    }

    private TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Unknown timezone '{TimeZoneId}', falling back to UTC", timeZoneId);
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            _logger.LogWarning("Invalid timezone '{TimeZoneId}', falling back to UTC", timeZoneId);
            return TimeZoneInfo.Utc;
        }
    }
}
