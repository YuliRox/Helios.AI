using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFullBrightnessDurationToRampProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FullBrightnessDurationSeconds",
                table: "ramp_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 900);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ramp_profiles_full_brightness_duration_non_negative",
                table: "ramp_profiles",
                sql: "\"FullBrightnessDurationSeconds\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ramp_profiles_full_brightness_duration_non_negative",
                table: "ramp_profiles");

            migrationBuilder.DropColumn(
                name: "FullBrightnessDurationSeconds",
                table: "ramp_profiles");
        }
    }
}
