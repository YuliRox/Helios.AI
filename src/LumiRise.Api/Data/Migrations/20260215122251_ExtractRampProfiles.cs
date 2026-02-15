using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtractRampProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultRampProfileId = new Guid("11111111-1111-1111-1111-111111111111");

            migrationBuilder.CreateTable(
                name: "ramp_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartBrightnessPercent = table.Column<int>(type: "integer", nullable: false),
                    TargetBrightnessPercent = table.Column<int>(type: "integer", nullable: false),
                    RampDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ramp_profiles", x => x.Id);
                    table.CheckConstraint("CK_ramp_profiles_ramp_duration_positive", "\"RampDurationSeconds\" > 0");
                    table.CheckConstraint("CK_ramp_profiles_start_brightness_range", "\"StartBrightnessPercent\" >= 0 AND \"StartBrightnessPercent\" <= 100");
                    table.CheckConstraint("CK_ramp_profiles_target_brightness_range", "\"TargetBrightnessPercent\" >= 0 AND \"TargetBrightnessPercent\" <= 100");
                });

            migrationBuilder.InsertData(
                table: "ramp_profiles",
                columns: new[]
                {
                    "Id",
                    "Mode",
                    "StartBrightnessPercent",
                    "TargetBrightnessPercent",
                    "RampDurationSeconds",
                    "CreatedAtUtc",
                    "UpdatedAtUtc"
                },
                values: new object[]
                {
                    defaultRampProfileId,
                    "default",
                    20,
                    100,
                    1800,
                    new DateTime(2026, 2, 15, 12, 22, 0, DateTimeKind.Utc),
                    new DateTime(2026, 2, 15, 12, 22, 0, DateTimeKind.Utc)
                });

            migrationBuilder.AddColumn<Guid>(
                name: "RampProfileId",
                table: "alarm_schedules",
                type: "uuid",
                nullable: false,
                defaultValue: defaultRampProfileId);

            migrationBuilder.DropCheckConstraint(
                name: "CK_alarm_schedules_ramp_duration_positive",
                table: "alarm_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_alarm_schedules_start_brightness_range",
                table: "alarm_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_alarm_schedules_target_brightness_range",
                table: "alarm_schedules");

            migrationBuilder.DropColumn(
                name: "RampDurationSeconds",
                table: "alarm_schedules");

            migrationBuilder.DropColumn(
                name: "StartBrightnessPercent",
                table: "alarm_schedules");

            migrationBuilder.DropColumn(
                name: "TargetBrightnessPercent",
                table: "alarm_schedules");

            migrationBuilder.CreateIndex(
                name: "IX_alarm_schedules_RampProfileId",
                table: "alarm_schedules",
                column: "RampProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ramp_profiles_Mode",
                table: "ramp_profiles",
                column: "Mode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_alarm_schedules_ramp_profiles_RampProfileId",
                table: "alarm_schedules",
                column: "RampProfileId",
                principalTable: "ramp_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alarm_schedules_ramp_profiles_RampProfileId",
                table: "alarm_schedules");

            migrationBuilder.DropTable(
                name: "ramp_profiles");

            migrationBuilder.DropIndex(
                name: "IX_alarm_schedules_RampProfileId",
                table: "alarm_schedules");

            migrationBuilder.DropColumn(
                name: "RampProfileId",
                table: "alarm_schedules");

            migrationBuilder.AddColumn<int>(
                name: "RampDurationSeconds",
                table: "alarm_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 1800);

            migrationBuilder.AddColumn<int>(
                name: "StartBrightnessPercent",
                table: "alarm_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "TargetBrightnessPercent",
                table: "alarm_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 100);

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
    }
}
