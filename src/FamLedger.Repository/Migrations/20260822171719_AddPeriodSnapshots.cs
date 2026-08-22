using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "period_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Income = table.Column<decimal>(type: "numeric", nullable: false),
                    TopUps = table.Column<decimal>(type: "numeric", nullable: false),
                    PlannedExpenses = table.Column<decimal>(type: "numeric", nullable: false),
                    Spent = table.Column<decimal>(type: "numeric", nullable: false),
                    Remaining = table.Column<decimal>(type: "numeric", nullable: false),
                    DailyBudget = table.Column<decimal>(type: "numeric", nullable: false),
                    DaysInPeriod = table.Column<int>(type: "integer", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    ExpenseCount = table.Column<int>(type: "integer", nullable: false),
                    IncomeCount = table.Column<int>(type: "integer", nullable: false),
                    CategoryBreakdownJson = table.Column<string>(type: "jsonb", nullable: false),
                    DailyBreakdownJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_period_snapshots_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_period_snapshots_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_period_snapshots_users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_period_snapshots_ClosedByUserId",
                table: "period_snapshots",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_period_snapshots_ContextId",
                table: "period_snapshots",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_period_snapshots_PeriodId",
                table: "period_snapshots",
                column: "PeriodId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "period_snapshots");
        }
    }
}
