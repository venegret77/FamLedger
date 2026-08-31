using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ClearSavingsAndReconciliationSavingsPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SavingsPlanJson",
                table: "period_reconciliations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.Sql(
                """
                DELETE FROM goal_contributions;
                UPDATE goals SET "IsCompleted" = FALSE, "CompletedAt" = NULL;
                DELETE FROM savings_deposits;
                UPDATE savings_entries SET "PlannedAmount" = 0, "ActualAmount" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SavingsPlanJson",
                table: "period_reconciliations");
        }
    }
}
