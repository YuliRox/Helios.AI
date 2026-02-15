using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Storage;
using LumiRise.Api.Data;
using LumiRise.Api.Data.Entities;
using LumiRise.Api.Services.Alarm.Implementation;
using LumiRise.Api.Services.Alarm.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;

namespace LumiRise.IntegrationTests.Services.Alarm;

public class HangfireAlarmSchedulingIntegrationTests(ITestOutputHelper testOutput)
    : ContainerTest<PostgreSqlBuilder, PostgreSqlContainer>(testOutput)
{
    protected override PostgreSqlBuilder Configure()
        => new PostgreSqlBuilder("postgres:16-alpine");

    [Fact]
    public async Task SyncAsync_AddsUpdatesAndRemovesRecurringJobs_FromDatabase()
    {
        var services = BuildServiceCollection(Container.GetConnectionString());
        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LumiRiseDbContext>();
            await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        var alarmId = Guid.NewGuid();
        await SeedAlarmAsync(provider, new AlarmScheduleEntity
        {
            Id = alarmId,
            Name = "Wake up",
            Enabled = true,
            CronExpression = "0 6 * * 1-5",
            TimeZoneId = "UTC",
            StartBrightnessPercent = 15,
            TargetBrightnessPercent = 100,
            RampDurationSeconds = 900
        });

        var synchronizer = provider.GetRequiredService<IAlarmRecurringJobSynchronizer>();
        await synchronizer.SyncAsync(TestContext.Current.CancellationToken);

        var recurringJobId = AlarmRecurringJobSynchronizer.BuildRecurringJobId(alarmId);
        var recurringJobs = GetAlarmRecurringJobs(provider).ToDictionary(x => x.Id, StringComparer.Ordinal);
        Assert.True(recurringJobs.ContainsKey(recurringJobId));
        Assert.Equal("0 6 * * 1-5", recurringJobs[recurringJobId].Cron);

        await UpdateAlarmAsync(provider, alarmId, alarm =>
        {
            alarm.CronExpression = "30 6 * * 1-5";
            alarm.UpdatedAtUtc = DateTime.UtcNow;
        });
        await synchronizer.SyncAsync(TestContext.Current.CancellationToken);

        recurringJobs = GetAlarmRecurringJobs(provider).ToDictionary(x => x.Id, StringComparer.Ordinal);
        Assert.True(recurringJobs.ContainsKey(recurringJobId));
        Assert.Equal("30 6 * * 1-5", recurringJobs[recurringJobId].Cron);

        await UpdateAlarmAsync(provider, alarmId, alarm =>
        {
            alarm.Enabled = false;
            alarm.UpdatedAtUtc = DateTime.UtcNow;
        });
        await synchronizer.SyncAsync(TestContext.Current.CancellationToken);

        recurringJobs = GetAlarmRecurringJobs(provider).ToDictionary(x => x.Id, StringComparer.Ordinal);
        Assert.False(recurringJobs.ContainsKey(recurringJobId));
    }

    private static ServiceCollection BuildServiceCollection(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<LumiRiseDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddHangfire(configuration => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(storageOptions =>
                storageOptions.UseNpgsqlConnection(connectionString)));

        services.AddSingleton<IAlarmRecurringJobSynchronizer, AlarmRecurringJobSynchronizer>();
        return services;
    }

    private static IReadOnlyCollection<RecurringJobDto> GetAlarmRecurringJobs(IServiceProvider provider)
    {
        var storage = provider.GetRequiredService<JobStorage>();
        using var connection = storage.GetConnection();
        return connection.GetRecurringJobs()
            .Where(x => x.Id.StartsWith("alarm:", StringComparison.Ordinal))
            .ToArray();
    }

    private static async Task SeedAlarmAsync(IServiceProvider provider, AlarmScheduleEntity entity)
    {
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LumiRiseDbContext>();
        dbContext.AlarmSchedules.Add(entity);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task UpdateAlarmAsync(IServiceProvider provider, Guid alarmId, Action<AlarmScheduleEntity> update)
    {
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LumiRiseDbContext>();
        var entity = await dbContext.AlarmSchedules.FirstAsync(
            x => x.Id == alarmId,
            TestContext.Current.CancellationToken);
        update(entity);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
