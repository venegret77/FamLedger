using FamLedger.Repository;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261026193000_ReminderSchedules")]
    public partial class ReminderSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastFiredAtUtc",
                table: "reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleJson",
                table: "reminders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE reminders
                SET "LastFiredAtUtc" = (("LastFiredDateUtc" + "TimeUtc") AT TIME ZONE 'UTC')
                WHERE "LastFiredDateUtc" IS NOT NULL
                  AND "TimeUtc" IS NOT NULL
                  AND "LastFiredAtUtc" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFiredAtUtc",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "ScheduleJson",
                table: "reminders");
        }
    }
}
