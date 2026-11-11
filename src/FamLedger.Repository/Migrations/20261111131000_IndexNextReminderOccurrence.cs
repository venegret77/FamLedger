using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FamLedger.Repository.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261111131000_IndexNextReminderOccurrence")]
public class IndexNextReminderOccurrence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("NextDueAtUtc", "reminders", type: "timestamp with time zone", nullable: true);
        migrationBuilder.DropIndex("IX_reminders_IsEnabled_TimeUtc", "reminders");
        migrationBuilder.CreateIndex("IX_reminders_IsEnabled_NextDueAtUtc", "reminders", new[] { "IsEnabled", "NextDueAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_reminders_IsEnabled_NextDueAtUtc", "reminders");
        migrationBuilder.DropColumn("NextDueAtUtc", "reminders");
        migrationBuilder.CreateIndex("IX_reminders_IsEnabled_TimeUtc", "reminders", new[] { "IsEnabled", "TimeUtc" });
    }
}
