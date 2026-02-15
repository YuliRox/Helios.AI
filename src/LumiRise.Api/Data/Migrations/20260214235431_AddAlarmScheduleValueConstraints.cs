using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlarmScheduleValueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_alarm_schedules_ramp_duration_positive",
                table: "alarm_schedules",
                sql: "\"RampDurationSeconds\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_alarm_schedules_start_brightness_range",
                table: "alarm_schedules",
                sql: "\"StartBrightnessPercent\" >= 0 AND \"StartBrightnessPercent\" <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_alarm_schedules_target_brightness_range",
                table: "alarm_schedules",
                sql: "\"TargetBrightnessPercent\" >= 0 AND \"TargetBrightnessPercent\" <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_alarm_schedules_ramp_duration_positive",
                table: "alarm_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_alarm_schedules_start_brightness_range",
                table: "alarm_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_alarm_schedules_target_brightness_range",
                table: "alarm_schedules");
        }
    }
}
