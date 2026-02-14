using LumiRise.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LumiRise.Api.Data;

public sealed class LumiRiseDbContext(DbContextOptions<LumiRiseDbContext> options) : DbContext(options)
{
    public DbSet<AlarmScheduleEntity> AlarmSchedules => Set<AlarmScheduleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var alarm = modelBuilder.Entity<AlarmScheduleEntity>();
        alarm.ToTable(
            "alarm_schedules",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_alarm_schedules_ramp_duration_positive",
                    "\"RampDurationSeconds\" > 0");
                tableBuilder.HasCheckConstraint(
                    "CK_alarm_schedules_start_brightness_range",
                    "\"StartBrightnessPercent\" >= 0 AND \"StartBrightnessPercent\" <= 100");
                tableBuilder.HasCheckConstraint(
                    "CK_alarm_schedules_target_brightness_range",
                    "\"TargetBrightnessPercent\" >= 0 AND \"TargetBrightnessPercent\" <= 100");
            });
        alarm.HasKey(x => x.Id);

        alarm.Property(x => x.Name).HasMaxLength(200).IsRequired();
        alarm.Property(x => x.CronExpression).HasMaxLength(120).IsRequired();
        alarm.Property(x => x.TimeZoneId).HasMaxLength(200).IsRequired();
        alarm.Property(x => x.StartBrightnessPercent).IsRequired();
        alarm.Property(x => x.TargetBrightnessPercent).IsRequired();
        alarm.Property(x => x.RampDurationSeconds).IsRequired();
        alarm.Property(x => x.CreatedAtUtc).IsRequired();
        alarm.Property(x => x.UpdatedAtUtc).IsRequired();

        alarm.HasIndex(x => x.Enabled);
    }
}
