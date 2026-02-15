using LumiRise.Api.Configuration;
using LumiRise.Api.Data;
using LumiRise.Api.Data.Entities;
using LumiRise.Api.Models.Alarms;
using LumiRise.Api.Services.Alarm.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace LumiRise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AlarmsController(
    LumiRiseDbContext dbContext,
    IAlarmRecurringJobSynchronizer alarmRecurringJobSynchronizer,
    IOptions<AlarmSettingsOptions> alarmSettingsOptions) : ControllerBase
{
    private readonly AlarmSettingsOptions _alarmSettings = alarmSettingsOptions.Value;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<AlarmResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AlarmResponse>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var alarms = await dbContext.AlarmSchedules
            .AsNoTracking()
            .Include(x => x.RampProfile)
            .OrderBy(x => x.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return Ok(alarms.Select(MapResponse).ToArray());
    }

    [HttpGet("{id:guid}", Name = "GetAlarmById")]
    [ProducesResponseType(typeof(AlarmResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlarmResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var alarm = await dbContext.AlarmSchedules
            .AsNoTracking()
            .Include(x => x.RampProfile)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return alarm is null ? NotFound() : Ok(MapResponse(alarm));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AlarmResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AlarmResponse>> CreateAsync(
        [FromBody] AlarmUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateRequest(request, out var time, out var daysOfWeek, out var rampMode))
        {
            return ValidationProblem(ModelState);
        }

        var rampProfile = await dbContext.RampProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Mode == rampMode, cancellationToken);
        if (rampProfile is null)
        {
            ModelState.AddModelError(nameof(request.RampMode), $"RampMode '{request.RampMode}' does not exist.");
            return ValidationProblem(ModelState);
        }

        if (!await ValidateNoScheduleOverlapAsync(
            currentAlarmId: null,
            request.Enabled,
            time,
            daysOfWeek,
            rampProfile,
            cancellationToken))
        {
            return ValidationProblem(ModelState);
        }

        var utcNow = DateTime.UtcNow;
        var entity = new AlarmScheduleEntity
        {
            Name = request.Name.Trim(),
            Enabled = request.Enabled,
            CronExpression = BuildWeeklyCronExpression(time, daysOfWeek),
            TimeZoneId = _alarmSettings.TimeZoneId.Trim(),
            RampProfileId = rampProfile.Id,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        dbContext.AlarmSchedules.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await alarmRecurringJobSynchronizer.SyncAsync(cancellationToken);

        return CreatedAtRoute("GetAlarmById", new { id = entity.Id }, MapResponse(entity, rampProfile.Mode));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AlarmResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlarmResponse>> UpdateAsync(
        Guid id,
        [FromBody] AlarmUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateRequest(request, out var time, out var daysOfWeek, out var rampMode))
        {
            return ValidationProblem(ModelState);
        }

        var rampProfile = await dbContext.RampProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Mode == rampMode, cancellationToken);
        if (rampProfile is null)
        {
            ModelState.AddModelError(nameof(request.RampMode), $"RampMode '{request.RampMode}' does not exist.");
            return ValidationProblem(ModelState);
        }

        if (!await ValidateNoScheduleOverlapAsync(
            currentAlarmId: id,
            request.Enabled,
            time,
            daysOfWeek,
            rampProfile,
            cancellationToken))
        {
            return ValidationProblem(ModelState);
        }

        var entity = await dbContext.AlarmSchedules
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Name = request.Name.Trim();
        entity.Enabled = request.Enabled;
        entity.CronExpression = BuildWeeklyCronExpression(time, daysOfWeek);
        entity.TimeZoneId = _alarmSettings.TimeZoneId.Trim();
        entity.RampProfileId = rampProfile.Id;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await alarmRecurringJobSynchronizer.SyncAsync(cancellationToken);

        return Ok(MapResponse(entity, rampProfile.Mode));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AlarmSchedules
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        dbContext.AlarmSchedules.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await alarmRecurringJobSynchronizer.SyncAsync(cancellationToken);

        return NoContent();
    }

    private bool ValidateRequest(
        AlarmUpsertRequest request,
        out TimeOnly time,
        out IReadOnlyCollection<DayOfWeek> daysOfWeek,
        out string rampMode)
    {
        time = default;
        daysOfWeek = [];
        rampMode = string.Empty;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
        }

        if (request.DaysOfWeek is null || request.DaysOfWeek.Count == 0)
        {
            ModelState.AddModelError(nameof(request.DaysOfWeek), "At least one weekday is required.");
        }
        else
        {
            var parsedDays = new HashSet<DayOfWeek>();
            foreach (var dayText in request.DaysOfWeek)
            {
                if (string.IsNullOrWhiteSpace(dayText))
                {
                    ModelState.AddModelError(nameof(request.DaysOfWeek), "Weekday values cannot be empty.");
                    continue;
                }

                if (!Enum.TryParse<DayOfWeek>(dayText.Trim(), ignoreCase: true, out var parsedDay))
                {
                    ModelState.AddModelError(
                        nameof(request.DaysOfWeek),
                        $"Weekday '{dayText}' is invalid. Use Sunday..Saturday.");
                    continue;
                }

                parsedDays.Add(parsedDay);
            }

            daysOfWeek = parsedDays.OrderBy(ToCronDayNumber).ToArray();
        }

        if (string.IsNullOrWhiteSpace(request.Time))
        {
            ModelState.AddModelError(nameof(request.Time), "Time is required in HH:mm format.");
        }
        else if (!TimeOnly.TryParseExact(
            request.Time.Trim(),
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time))
        {
            ModelState.AddModelError(nameof(request.Time), "Time must be in HH:mm format.");
        }

        if (string.IsNullOrWhiteSpace(request.RampMode))
        {
            ModelState.AddModelError(nameof(request.RampMode), "RampMode is required.");
        }
        else
        {
            rampMode = request.RampMode.Trim().ToLowerInvariant();
        }

        return ModelState.IsValid;
    }

    private async Task<bool> ValidateNoScheduleOverlapAsync(
        Guid? currentAlarmId,
        bool enabled,
        TimeOnly startTime,
        IReadOnlyCollection<DayOfWeek> daysOfWeek,
        RampProfileEntity rampProfile,
        CancellationToken cancellationToken)
    {
        if (!enabled || daysOfWeek.Count == 0)
        {
            return true;
        }

        var targetDays = daysOfWeek.ToHashSet();
        var targetStart = ToTimeSpan(startTime);
        var targetEnd = targetStart + TimeSpan.FromSeconds(rampProfile.RampDurationSeconds);

        var existingEnabledAlarms = await dbContext.AlarmSchedules
            .AsNoTracking()
            .Where(x => x.Enabled)
            .Where(x => !currentAlarmId.HasValue || x.Id != currentAlarmId.Value)
            .Include(x => x.RampProfile)
            .ToArrayAsync(cancellationToken);

        foreach (var existing in existingEnabledAlarms)
        {
            var (existingTime, existingDays) = ParseScheduleFromCronExpression(existing.CronExpression);
            if (existingDays.Count == 0)
            {
                continue;
            }

            if (!existingDays.Any(targetDays.Contains))
            {
                continue;
            }

            var existingStart = ToTimeSpan(existingTime);
            var existingEnd = existingStart + TimeSpan.FromSeconds(existing.RampProfile.RampDurationSeconds);

            if (TimeSlotsOverlap(targetStart, targetEnd, existingStart, existingEnd))
            {
                ModelState.AddModelError(
                    nameof(AlarmUpsertRequest.Time),
                    $"Schedule overlaps with existing alarm '{existing.Name}' ({existingStart:hh\\:mm}-{existingEnd:hh\\:mm}).");
                return false;
            }
        }

        return true;
    }

    private static AlarmResponse MapResponse(AlarmScheduleEntity entity)
    {
        if (entity.RampProfile is null)
        {
            throw new InvalidOperationException($"Alarm '{entity.Id}' has no ramp profile loaded.");
        }

        return MapResponse(entity, entity.RampProfile.Mode);
    }

    private static AlarmResponse MapResponse(AlarmScheduleEntity entity, string rampMode)
    {
        var (time, daysOfWeek) = ParseScheduleFromCronExpression(entity.CronExpression);

        return new AlarmResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Enabled = entity.Enabled,
            DaysOfWeek = daysOfWeek.Select(x => x.ToString()).ToArray(),
            Time = time.ToString("HH:mm", CultureInfo.InvariantCulture),
            RampMode = rampMode,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static string BuildWeeklyCronExpression(TimeOnly time, IReadOnlyCollection<DayOfWeek> daysOfWeek)
    {
        var dayPart = string.Join(
            ",",
            daysOfWeek
                .OrderBy(ToCronDayNumber)
                .Select(x => ToCronDayNumber(x).ToString(CultureInfo.InvariantCulture)));

        return $"{time.Minute} {time.Hour} * * {dayPart}";
    }

    private static (TimeOnly Time, IReadOnlyCollection<DayOfWeek> DaysOfWeek) ParseScheduleFromCronExpression(
        string cronExpression)
    {
        var segments = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 5)
        {
            throw new InvalidOperationException($"Cron expression '{cronExpression}' is not in 5-part format.");
        }

        if (!int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minute)
            || minute is < 0 or > 59)
        {
            throw new InvalidOperationException($"Cron expression '{cronExpression}' has invalid minute.");
        }

        if (!int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            || hour is < 0 or > 23)
        {
            throw new InvalidOperationException($"Cron expression '{cronExpression}' has invalid hour.");
        }

        if (segments[2] != "*" || segments[3] != "*")
        {
            return (new TimeOnly(hour, minute), []);
        }

        IReadOnlyCollection<DayOfWeek> daysOfWeek;
        try
        {
            daysOfWeek = ParseCronDaysOfWeek(segments[4]);
        }
        catch (InvalidOperationException)
        {
            daysOfWeek = [];
        }

        return (new TimeOnly(hour, minute), daysOfWeek);
    }

    private static IReadOnlyCollection<DayOfWeek> ParseCronDaysOfWeek(string dayPart)
    {
        if (dayPart == "*")
        {
            return
            [
                DayOfWeek.Sunday,
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday
            ];
        }

        var days = new HashSet<DayOfWeek>();
        var segments = dayPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('-', StringComparison.Ordinal);
            if (separatorIndex >= 0)
            {
                var startDay = ParseCronDayToken(segment[..separatorIndex]);
                var endDay = ParseCronDayToken(segment[(separatorIndex + 1)..]);
                var startNumber = ToCronDayNumber(startDay);
                var endNumber = ToCronDayNumber(endDay);

                if (startNumber > endNumber)
                {
                    throw new InvalidOperationException($"Cron day range '{segment}' is invalid.");
                }

                for (var dayNumber = startNumber; dayNumber <= endNumber; dayNumber++)
                {
                    days.Add(FromCronDayNumber(dayNumber));
                }
            }
            else
            {
                days.Add(ParseCronDayToken(segment));
            }
        }

        return days.OrderBy(ToCronDayNumber).ToArray();
    }

    private static DayOfWeek ParseCronDayToken(string token)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericDay))
        {
            return numericDay switch
            {
                >= 0 and <= 6 => FromCronDayNumber(numericDay),
                7 => DayOfWeek.Sunday,
                _ => throw new InvalidOperationException($"Cron day token '{token}' is invalid.")
            };
        }

        if (Enum.TryParse<DayOfWeek>(token, ignoreCase: true, out var enumDay))
        {
            return enumDay;
        }

        return token.ToUpperInvariant() switch
        {
            "SUN" => DayOfWeek.Sunday,
            "MON" => DayOfWeek.Monday,
            "TUE" or "TUES" => DayOfWeek.Tuesday,
            "WED" => DayOfWeek.Wednesday,
            "THU" or "THUR" or "THURS" => DayOfWeek.Thursday,
            "FRI" => DayOfWeek.Friday,
            "SAT" => DayOfWeek.Saturday,
            _ => throw new InvalidOperationException($"Cron day token '{token}' is invalid.")
        };
    }

    private static int ToCronDayNumber(DayOfWeek dayOfWeek) => (int)dayOfWeek;

    private static TimeSpan ToTimeSpan(TimeOnly time) => time.ToTimeSpan();

    private static bool TimeSlotsOverlap(
        TimeSpan firstStart,
        TimeSpan firstEnd,
        TimeSpan secondStart,
        TimeSpan secondEnd)
        => firstStart < secondEnd && secondStart < firstEnd;

    private static DayOfWeek FromCronDayNumber(int dayNumber)
        => dayNumber switch
        {
            >= 0 and <= 6 => (DayOfWeek)dayNumber,
            _ => throw new InvalidOperationException($"Day number '{dayNumber}' is invalid.")
        };
}
