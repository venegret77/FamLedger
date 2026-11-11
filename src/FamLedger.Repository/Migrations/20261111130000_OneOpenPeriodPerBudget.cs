using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FamLedger.Repository.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261111130000_OneOpenPeriodPerBudget")]
public class OneOpenPeriodPerBudget : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM budget_periods WHERE NOT "IsClosed"
                    GROUP BY "ContextId" HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Multiple open FamLedger periods exist for a budget. Reconcile them before upgrading.';
                END IF;
            END $$;
            """);
        migrationBuilder.CreateIndex(
            name: "IX_budget_periods_ContextId",
            table: "budget_periods",
            column: "ContextId",
            unique: true,
            filter: "NOT \"IsClosed\"");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex("IX_budget_periods_ContextId", "budget_periods");
}
