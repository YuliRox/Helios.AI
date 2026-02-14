using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAlarmSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alarm_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartBrightnessPercent = table.Column<int>(type: "integer", nullable: false),
                    TargetBrightnessPercent = table.Column<int>(type: "integer", nullable: false),
                    RampDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alarm_schedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alarm_schedules_Enabled",
                table: "alarm_schedules",
                column: "Enabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alarm_schedules");
        }
    }
}
