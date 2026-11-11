using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FamLedger.Repository.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261111130000_OneOpenPeriodPerBudget")]
public class OneOpenPeriodPerBudget : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateIndex(
            name: "IX_budget_periods_ContextId",
            table: "budget_periods",
            column: "ContextId",
            filter: "NOT \"IsClosed\"");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex("IX_budget_periods_ContextId", "budget_periods");
}
