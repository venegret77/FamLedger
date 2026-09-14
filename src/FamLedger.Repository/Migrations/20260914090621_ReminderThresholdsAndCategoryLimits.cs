using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ReminderThresholdsAndCategoryLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "ThresholdPercents",
                table: "reminders",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.Sql("""
                UPDATE reminders
                SET "ThresholdPercents" = CASE
                    WHEN "Kind" = 2 THEN ARRAY[COALESCE("ThresholdPercent", 80)]
                    ELSE ARRAY[]::integer[]
                END;
                """);

            migrationBuilder.DropColumn(
                name: "ThresholdPercent",
                table: "reminders");

            migrationBuilder.CreateTable(
                name: "category_spending_limits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LimitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ThresholdPercents = table.Column<int[]>(type: "integer[]", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_spending_limits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_category_spending_limits_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_category_spending_limits_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_category_spending_limits_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reminder_threshold_fires",
                columns: table => new
                {
                    ReminderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    LastFiredDateUtc = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder_threshold_fires", x => new { x.ReminderId, x.ThresholdPercent });
                    table.ForeignKey(
                        name: "FK_reminder_threshold_fires_reminders_ReminderId",
                        column: x => x.ReminderId,
                        principalTable: "reminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO reminder_threshold_fires ("ReminderId", "ThresholdPercent", "LastFiredDateUtc")
                SELECT r."Id", (r."ThresholdPercents")[1], r."LastFiredDateUtc"
                FROM reminders r
                WHERE r."Kind" = 2
                  AND r."LastFiredDateUtc" IS NOT NULL
                  AND cardinality(r."ThresholdPercents") > 0;
                """);

            migrationBuilder.CreateTable(
                name: "user_activity_states",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityKind = table.Column<int>(type: "integer", nullable: false),
                    LastRecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_activity_states", x => new { x.UserId, x.ContextId });
                    table.ForeignKey(
                        name: "FK_user_activity_states_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_activity_states_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "category_limit_threshold_fires",
                columns: table => new
                {
                    LimitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    LastFiredDateUtc = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_limit_threshold_fires", x => new { x.LimitId, x.ThresholdPercent });
                    table.ForeignKey(
                        name: "FK_category_limit_threshold_fires_category_spending_limits_Lim~",
                        column: x => x.LimitId,
                        principalTable: "category_spending_limits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_category_spending_limits_CategoryId",
                table: "category_spending_limits",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_category_spending_limits_ContextId_CategoryId",
                table: "category_spending_limits",
                columns: new[] { "ContextId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_spending_limits_CreatedByUserId",
                table: "category_spending_limits",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_activity_states_ContextId",
                table: "user_activity_states",
                column: "ContextId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_limit_threshold_fires");

            migrationBuilder.DropTable(
                name: "reminder_threshold_fires");

            migrationBuilder.DropTable(
                name: "user_activity_states");

            migrationBuilder.DropTable(
                name: "category_spending_limits");

            migrationBuilder.AddColumn<int>(
                name: "ThresholdPercent",
                table: "reminders",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE reminders
                SET "ThresholdPercent" = CASE
                    WHEN cardinality("ThresholdPercents") > 0 THEN ("ThresholdPercents")[1]
                    ELSE NULL
                END;
                """);

            migrationBuilder.DropColumn(
                name: "ThresholdPercents",
                table: "reminders");
        }
    }
}
