using LumiRise.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LumiRise.Api.Data;

public sealed class LumiRiseDbContext(DbContextOptions<LumiRiseDbContext> options) : DbContext(options)
{
    public DbSet<AlarmScheduleEntity> AlarmSchedules => Set<AlarmScheduleEntity>();
    public DbSet<RampProfileEntity> RampProfiles => Set<RampProfileEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var rampProfile = modelBuilder.Entity<RampProfileEntity>();
        rampProfile.ToTable(
            "ramp_profiles",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_ramp_profiles_ramp_duration_positive",
                    "\"RampDurationSeconds\" > 0");
                tableBuilder.HasCheckConstraint(
                    "CK_ramp_profiles_start_brightness_range",
                    "\"StartBrightnessPercent\" >= 0 AND \"StartBrightnessPercent\" <= 100");
                tableBuilder.HasCheckConstraint(
                    "CK_ramp_profiles_target_brightness_range",
                    "\"TargetBrightnessPercent\" >= 0 AND \"TargetBrightnessPercent\" <= 100");
            });
        rampProfile.HasKey(x => x.Id);
        rampProfile.Property(x => x.Mode).HasMaxLength(100).IsRequired();
        rampProfile.Property(x => x.StartBrightnessPercent).IsRequired();
        rampProfile.Property(x => x.TargetBrightnessPercent).IsRequired();
        rampProfile.Property(x => x.RampDurationSeconds).IsRequired();
        rampProfile.Property(x => x.CreatedAtUtc).IsRequired();
        rampProfile.Property(x => x.UpdatedAtUtc).IsRequired();
        rampProfile.HasIndex(x => x.Mode).IsUnique();

        var alarm = modelBuilder.Entity<AlarmScheduleEntity>();
        alarm.ToTable("alarm_schedules");
        alarm.HasKey(x => x.Id);

        alarm.Property(x => x.Name).HasMaxLength(200).IsRequired();
        alarm.Property(x => x.CronExpression).HasMaxLength(120).IsRequired();
        alarm.Property(x => x.TimeZoneId).HasMaxLength(200).IsRequired();
        alarm.Property(x => x.RampProfileId).IsRequired();
        alarm.Property(x => x.CreatedAtUtc).IsRequired();
        alarm.Property(x => x.UpdatedAtUtc).IsRequired();

        alarm.HasIndex(x => x.Enabled);
        alarm.HasIndex(x => x.RampProfileId);

        alarm.HasOne(x => x.RampProfile)
            .WithMany(x => x.AlarmSchedules)
            .HasForeignKey(x => x.RampProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
