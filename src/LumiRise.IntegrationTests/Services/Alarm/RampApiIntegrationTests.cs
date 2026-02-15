using LumiRise.Api.Data;
using LumiRise.Api.Models.Alarms;
using LumiRise.Api.Models.Ramps;
using LumiRise.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LumiRise.IntegrationTests.Services.Alarm;

public class RampApiIntegrationTests(ApiAlbaHostFixture fixture)
    : IClassFixture<ApiAlbaHostFixture>
{
    [Fact]
    public async Task CrudOperations_WorkEndToEnd()
    {
        await fixture.ResetDatabaseAsync();

        var createRequest = new RampUpsertRequest
        {
            Mode = "quick",
            StartBrightnessPercent = 40,
            TargetBrightnessPercent = 100,
            RampDurationSeconds = 600
        };

        await fixture.Host.Scenario(api =>
        {
            api.Post.Json(createRequest).ToUrl("/api/ramps");
            api.StatusCodeShouldBe(StatusCodes.Status201Created);
        });

        await using var createContext = CreateDbContext();
        var created = await createContext.RampProfiles.SingleAsync(
            x => x.Mode == "quick",
            TestContext.Current.CancellationToken);
        created.StartBrightnessPercent.Should().Be(40);
        created.TargetBrightnessPercent.Should().Be(100);
        created.RampDurationSeconds.Should().Be(600);

        await fixture.Host.Scenario(api =>
        {
            api.Get.Url($"/api/ramps/{created.Id}");
            api.StatusCodeShouldBe(StatusCodes.Status200OK);
        });

        var updateRequest = new RampUpsertRequest
        {
            Mode = "quick-ramp",
            StartBrightnessPercent = 30,
            TargetBrightnessPercent = 90,
            RampDurationSeconds = 300
        };

        await fixture.Host.Scenario(api =>
        {
            api.Put.Json(updateRequest).ToUrl($"/api/ramps/{created.Id}");
            api.StatusCodeShouldBe(StatusCodes.Status200OK);
        });

        await using var updateContext = CreateDbContext();
        var updated = await updateContext.RampProfiles.SingleAsync(
            x => x.Id == created.Id,
            TestContext.Current.CancellationToken);
        updated.Mode.Should().Be("quick-ramp");
        updated.StartBrightnessPercent.Should().Be(30);
        updated.TargetBrightnessPercent.Should().Be(90);
        updated.RampDurationSeconds.Should().Be(300);

        await fixture.Host.Scenario(api =>
        {
            api.Delete.Url($"/api/ramps/{created.Id}");
            api.StatusCodeShouldBe(StatusCodes.Status204NoContent);
        });

        await using var deleteContext = CreateDbContext();
        var deleted = await deleteContext.RampProfiles.FirstOrDefaultAsync(
            x => x.Id == created.Id,
            TestContext.Current.CancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenRampProfileIsUsedByAlarm()
    {
        await fixture.ResetDatabaseAsync();

        await fixture.Host.Scenario(api =>
        {
            api.Post.Json(new RampUpsertRequest
            {
                Mode = "quick",
                StartBrightnessPercent = 25,
                TargetBrightnessPercent = 100,
                RampDurationSeconds = 300
            }).ToUrl("/api/ramps");
            api.StatusCodeShouldBe(StatusCodes.Status201Created);
        });

        await fixture.Host.Scenario(api =>
        {
            api.Post.Json(new AlarmUpsertRequest
            {
                Name = "Uses quick ramp",
                Enabled = true,
                DaysOfWeek = ["Monday"],
                Time = "07:00",
                RampMode = "quick"
            }).ToUrl("/api/alarms");
            api.StatusCodeShouldBe(StatusCodes.Status201Created);
        });

        await using var dbContext = CreateDbContext();
        var inUseRamp = await dbContext.RampProfiles.SingleAsync(
            x => x.Mode == "quick",
            TestContext.Current.CancellationToken);

        await fixture.Host.Scenario(api =>
        {
            api.Delete.Url($"/api/ramps/{inUseRamp.Id}");
            api.StatusCodeShouldBe(StatusCodes.Status409Conflict);
        });
    }

    private LumiRiseDbContext CreateDbContext()
    {
        return fixture.CreateDbContext();
    }
}
