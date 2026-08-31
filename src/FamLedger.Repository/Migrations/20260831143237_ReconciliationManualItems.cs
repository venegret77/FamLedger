using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ReconciliationManualItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "period_reconciliations");

            migrationBuilder.CreateTable(
                name: "period_reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetItemsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    ObligationItemsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_reconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_period_reconciliations_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_period_reconciliations_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_period_reconciliations_ContextId_PeriodId",
                table: "period_reconciliations",
                columns: new[] { "ContextId", "PeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_period_reconciliations_PeriodId",
                table: "period_reconciliations",
                column: "PeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "period_reconciliations");

            migrationBuilder.CreateTable(
                name: "period_reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CashJson = table.Column<string>(type: "jsonb", nullable: false),
                    SetAsideJson = table.Column<string>(type: "jsonb", nullable: false),
                    ManualPlannedJson = table.Column<string>(type: "jsonb", nullable: false),
                    SavingsPlanJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_reconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_period_reconciliations_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_period_reconciliations_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_period_reconciliations_ContextId_PeriodId",
                table: "period_reconciliations",
                columns: new[] { "ContextId", "PeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_period_reconciliations_PeriodId",
                table: "period_reconciliations",
                column: "PeriodId");
        }
    }
}
