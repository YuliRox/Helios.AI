using LumiRise.Api.Data;
using LumiRise.Api.Models.Alarms;
using LumiRise.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LumiRise.IntegrationTests.Services.Alarm;

public class AlarmApiIntegrationTests(ApiAlbaHostFixture fixture)
    : IClassFixture<ApiAlbaHostFixture>
{
    [Fact]
    public async Task CrudOperations_WorkEndToEnd()
    {
        await fixture.ResetDatabaseAsync();

        var createRequest = new AlarmUpsertRequest
        {
            Name = "Weekday Wakeup",
            Enabled = true,
            DaysOfWeek = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
            Time = "06:00",
            RampMode = "default"
        };

        await fixture.Host.Scenario(api =>
        {
            api.Post.Json(createRequest).ToUrl("/api/alarms");
            api.StatusCodeShouldBe(StatusCodes.Status201Created);
        });

        await using var createdContext = CreateDbContext();
        var created = await createdContext.AlarmSchedules
            .Include(x => x.RampProfile)
            .SingleAsync(x => x.Name == createRequest.Name, TestContext.Current.CancellationToken);
        created.CronExpression.Should().Be("0 6 * * 1,2,3,4,5");
        created.TimeZoneId.Should().Be("UTC");
        created.RampProfile.Mode.Should().Be("default");
        created.RampProfile.StartBrightnessPercent.Should().Be(20);
        created.RampProfile.TargetBrightnessPercent.Should().Be(100);
        created.RampProfile.RampDurationSeconds.Should().Be(1800);

        await fixture.Host.Scenario(api =>
        {
            api.Get.Url($"/api/alarms/{created.Id}");
            api.StatusCodeShouldBe(StatusCodes.Status200OK);
        });

        var updateRequest = new AlarmUpsertRequest
        {
            Name = "Weekday Wakeup Updated",
            Enabled = false,
            DaysOfWeek = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
            Time = "06:30",
            RampMode = "default"
        };

        await fixture.Host.Scenario(api =>
        {
            api.Put.Json(updateRequest).ToUrl($"/api/alarms/{created.Id}");
            api.StatusCodeShouldBe(StatusCodes.Status200OK);
        });

        await using var updatedContext = CreateDbContext();
        var updated = await updatedContext.AlarmSchedules
            .Include(x => x.RampProfile)
            .SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);

        updated.Name.Should().Be(updateRequest.Name);
        updated.Enabled.Should().Be(updateRequest.Enabled);
        updated.CronExpression.Should().Be("30 6 * * 1,2,3,4,5");
        updated.TimeZoneId.Should().Be("UTC");
        updated.RampProfile.Mode.Should().Be("default");
        updated.RampProfile.StartBrightnessPercent.Should().Be(20);
        updated.RampProfile.TargetBrightnessPercent.Should().Be(100);
        updated.RampProfile.RampDurationSeconds.Should().Be(1800);

        await fixture.Host.Scenario(api =>
        {
            api.Delete.Url($"/api/alarms/{created.Id}");
            api.StatusCodeShouldBe(StatusCodes.Status204NoContent);
        });

        await using var deletedContext = CreateDbContext();
        var deleted = await deletedContext.AlarmSchedules.FirstOrDefaultAsync(
            x => x.Id == created.Id,
            TestContext.Current.CancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task SwaggerJsonAndUi_AreAvailable()
    {
        await fixture.ResetDatabaseAsync();

        await fixture.Host.Scenario(api =>
        {
            api.Get.Url("/swagger/v1/swagger.json");
            api.StatusCodeShouldBe(StatusCodes.Status200OK);
        });

        await fixture.Host.Scenario(api =>
        {
            api.Get.Url("/swagger/index.html");
            api.StatusCodeShouldBe(StatusCodes.Status200OK);
        });
    }

    [Fact]
    public async Task Create_RejectsOverlappingAlarmOnSameDay()
    {
        await fixture.ResetDatabaseAsync();

        var firstAlarm = new AlarmUpsertRequest
        {
            Name = "Alarm A",
            Enabled = true,
            DaysOfWeek = ["Monday"],
            Time = "12:35",
            RampMode = "default"
        };

        var overlappingAlarm = new AlarmUpsertRequest
        {
            Name = "Alarm B",
            Enabled = true,
            DaysOfWeek = ["Monday"],
            Time = "12:40",
            RampMode = "default"
        };

        await fixture.Host.Scenario(api =>
        {
            api.Post.Json(firstAlarm).ToUrl("/api/alarms");
            api.StatusCodeShouldBe(StatusCodes.Status201Created);
        });

        await fixture.Host.Scenario(api =>
        {
            api.Post.Json(overlappingAlarm).ToUrl("/api/alarms");
            api.StatusCodeShouldBe(StatusCodes.Status400BadRequest);
        });

        await using var dbContext = CreateDbContext();
        var count = await dbContext.AlarmSchedules.CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Update_RejectsOverlappingAlarmOnSameDay()
    {
        await fixture.ResetDatabaseAsync();

        var firstAlarm = new AlarmUpsertRequest
        {
            Name = "Alarm A",
            Enabled = true,
            DaysOfWeek = ["Monday"],
            Time = "12:35",
            RampMode = "default"
        };

        var secondAlarm = new AlarmUpsertRequest
        {
            Name = "Alarm B",
            Enabled = true,
            DaysOfWeek = ["Monday"],
            Time = "13:10",
            RampMode = "default"
        };

        await fixture.Host.Scenario(api =>
        {
            api.Post.Json(firstAlarm).ToUrl("/api/alarms");
            api.StatusCodeShouldBe(StatusCodes.Status201Created);
        });

        await fixture.Host.Scenario(api =>
        {
            api.Post.Json(secondAlarm).ToUrl("/api/alarms");
            api.StatusCodeShouldBe(StatusCodes.Status201Created);
        });

        await using var lookupContext = CreateDbContext();
        var alarmToUpdate = await lookupContext.AlarmSchedules
            .SingleAsync(x => x.Name == secondAlarm.Name, TestContext.Current.CancellationToken);

        var overlappingUpdate = new AlarmUpsertRequest
        {
            Name = "Alarm B",
            Enabled = true,
            DaysOfWeek = ["Monday"],
            Time = "12:40",
            RampMode = "default"
        };

        await fixture.Host.Scenario(api =>
        {
            api.Put.Json(overlappingUpdate).ToUrl($"/api/alarms/{alarmToUpdate.Id}");
            api.StatusCodeShouldBe(StatusCodes.Status400BadRequest);
        });

        await using var verifyContext = CreateDbContext();
        var unchangedAlarm = await verifyContext.AlarmSchedules
            .SingleAsync(x => x.Id == alarmToUpdate.Id, TestContext.Current.CancellationToken);
        unchangedAlarm.CronExpression.Should().Be("10 13 * * 1");
    }

    private LumiRiseDbContext CreateDbContext()
    {
        return fixture.CreateDbContext();
    }
}
