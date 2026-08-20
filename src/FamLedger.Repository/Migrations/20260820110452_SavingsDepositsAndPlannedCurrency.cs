using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SavingsDepositsAndPlannedCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "savings_entries",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "PlannedCurrency",
                table: "savings_entries",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "savings_deposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_deposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_savings_deposits_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_savings_deposits_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_savings_deposits_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_savings_deposits_ContextId_PeriodId",
                table: "savings_deposits",
                columns: new[] { "ContextId", "PeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_savings_deposits_PeriodId",
                table: "savings_deposits",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_savings_deposits_UserId",
                table: "savings_deposits",
                column: "UserId");

            migrationBuilder.Sql(
                """
                UPDATE savings_entries
                SET "PlannedCurrency" = "Currency"
                WHERE "PlannedCurrency" IS NULL OR "PlannedCurrency" = '';

                INSERT INTO savings_deposits ("Id", "ContextId", "PeriodId", "UserId", "Amount", "Currency", "CreatedAt")
                SELECT
                    gen_random_uuid(),
                    se."ContextId",
                    se."PeriodId",
                    (
                        SELECT cm."UserId"
                        FROM context_members AS cm
                        WHERE cm."ContextId" = se."ContextId"
                        ORDER BY cm."JoinedAt", cm."Id"
                        LIMIT 1
                    ),
                    se."ActualAmount",
                    se."Currency",
                    NOW()
                FROM savings_entries AS se
                WHERE se."ActualAmount" > 0
                  AND EXISTS (
                      SELECT 1
                      FROM context_members AS cm
                      WHERE cm."ContextId" = se."ContextId"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "savings_deposits");

            migrationBuilder.DropColumn(
                name: "PlannedCurrency",
                table: "savings_entries");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "savings_entries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);
        }
    }
}
