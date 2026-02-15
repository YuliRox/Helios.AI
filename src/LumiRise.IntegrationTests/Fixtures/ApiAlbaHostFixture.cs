using Alba;
using LumiRise.Api.Data;
using LumiRise.Api.Data.Entities;
using LumiRise.Api.Services.Alarm.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;

namespace LumiRise.IntegrationTests.Fixtures;

public sealed class ApiAlbaHostFixture()
    : ContainerTest<PostgreSqlBuilder, PostgreSqlContainer>(null!)
{
    public IAlbaHost Host { get; private set; } = null!;

    protected override PostgreSqlBuilder Configure()
        => new PostgreSqlBuilder("postgres:16-alpine");

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Postgres", Container.GetConnectionString());
        });

        await ResetDatabaseAsync();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (Host is not null)
        {
            await Host.DisposeAsync();
        }

        await base.DisposeAsyncCore();
    }

    public LumiRiseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LumiRiseDbContext>()
            .UseNpgsql(Container.GetConnectionString())
            .Options;

        return new LumiRiseDbContext(options);
    }

    public async Task ResetDatabaseAsync()
    {
        await using var dbContext = CreateDbContext();

        await dbContext.AlarmSchedules.ExecuteDeleteAsync(CancellationToken.None);
        await dbContext.RampProfiles.ExecuteDeleteAsync(CancellationToken.None);

        var utcNow = DateTime.UtcNow;
        dbContext.RampProfiles.Add(new RampProfileEntity
        {
            Mode = RampProfileEntity.DefaultMode,
            StartBrightnessPercent = RampProfileEntity.DefaultStartBrightnessPercent,
            TargetBrightnessPercent = RampProfileEntity.DefaultTargetBrightnessPercent,
            RampDurationSeconds = RampProfileEntity.DefaultRampDurationSeconds,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        await dbContext.SaveChangesAsync(CancellationToken.None);
        await SyncRecurringJobsAsync();
    }

    private async Task SyncRecurringJobsAsync()
    {
        using var scope = Host.Services.CreateScope();
        var synchronizer = scope.ServiceProvider.GetRequiredService<IAlarmRecurringJobSynchronizer>();
        await synchronizer.SyncAsync(CancellationToken.None);
    }
}
