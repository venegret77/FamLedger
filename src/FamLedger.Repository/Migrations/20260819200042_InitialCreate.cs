using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "budget_contexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsPersonal = table.Column<bool>(type: "boolean", nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PeriodStartDay = table.Column<int>(type: "integer", nullable: false),
                    InviteCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_contexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RateToRsd = table.Column<decimal>(type: "numeric", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "budget_periods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CarryoverBase = table.Column<decimal>(type: "numeric", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_periods_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_categories_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goals_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ReceivedRate = table.Column<decimal>(type: "numeric", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incomes_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AvatarKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ActiveContextId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_budget_contexts_ActiveContextId",
                        column: x => x.ActiveContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "one_off_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_one_off_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_one_off_expenses_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_one_off_expenses_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rate_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    RateToRsd = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rate_overrides_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rate_overrides_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "savings_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_savings_entries_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_savings_entries_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefinitionAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    DefinitionCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ChargeDayOfMonth = table.Column<int>(type: "integer", nullable: false),
                    RecalcByRate = table.Column<bool>(type: "boolean", nullable: false),
                    SourceIncomeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recurring_expenses_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_expenses_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "context_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_context_members_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_context_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "debts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CounterpartyUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_debts_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_debts_users_CounterpartyUserId",
                        column: x => x.CounterpartyUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "goal_contributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_contributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goal_contributions_goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_goal_contributions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "join_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_join_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_join_requests_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_join_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    P256dh = table.Column<string>(type: "text", nullable: false),
                    Auth = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_subscriptions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transactions_budget_contexts_ContextId",
                        column: x => x.ContextId,
                        principalTable: "budget_contexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transactions_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transactions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_transactions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_endpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Secret = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_endpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_endpoints_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "period_recurring_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedBaseAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_recurring_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_period_recurring_items_budget_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "budget_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_period_recurring_items_recurring_expenses_RecurringExpenseId",
                        column: x => x.RecurringExpenseId,
                        principalTable: "recurring_expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "debt_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DebtId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debt_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_debt_entries_debts_DebtId",
                        column: x => x.DebtId,
                        principalTable: "debts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budget_contexts_InviteCode",
                table: "budget_contexts",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_periods_ContextId_StartDate",
                table: "budget_periods",
                columns: new[] { "ContextId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_ContextId",
                table: "categories",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_context_members_ContextId_UserId",
                table: "context_members",
                columns: new[] { "ContextId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_context_members_UserId",
                table: "context_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_debt_entries_DebtId",
                table: "debt_entries",
                column: "DebtId");

            migrationBuilder.CreateIndex(
                name: "IX_debts_ContextId",
                table: "debts",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_debts_CounterpartyUserId",
                table: "debts",
                column: "CounterpartyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rates_Date_Currency",
                table: "exchange_rates",
                columns: new[] { "Date", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goal_contributions_GoalId",
                table: "goal_contributions",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_goal_contributions_UserId",
                table: "goal_contributions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_goals_ContextId",
                table: "goals",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_incomes_ContextId",
                table: "incomes",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_ContextId",
                table: "join_requests",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_UserId",
                table: "join_requests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_subscriptions_UserId",
                table: "notification_subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_one_off_expenses_ContextId",
                table: "one_off_expenses",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_one_off_expenses_PeriodId",
                table: "one_off_expenses",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_period_recurring_items_PeriodId_RecurringExpenseId",
                table: "period_recurring_items",
                columns: new[] { "PeriodId", "RecurringExpenseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_period_recurring_items_RecurringExpenseId",
                table: "period_recurring_items",
                column: "RecurringExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_rate_overrides_ContextId",
                table: "rate_overrides",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_rate_overrides_PeriodId",
                table: "rate_overrides",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expenses_CategoryId",
                table: "recurring_expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expenses_ContextId",
                table: "recurring_expenses",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_savings_entries_ContextId_PeriodId",
                table: "savings_entries",
                columns: new[] { "ContextId", "PeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_savings_entries_PeriodId",
                table: "savings_entries",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CategoryId",
                table: "transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_ContextId",
                table: "transactions",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CreatedByUserId",
                table: "transactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_PeriodId_Date",
                table: "transactions",
                columns: new[] { "PeriodId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_users_ActiveContextId",
                table: "users",
                column: "ActiveContextId");

            migrationBuilder.CreateIndex(
                name: "IX_users_TelegramUserId",
                table: "users",
                column: "TelegramUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_UserId",
                table: "webhook_endpoints",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "context_members");

            migrationBuilder.DropTable(
                name: "debt_entries");

            migrationBuilder.DropTable(
                name: "exchange_rates");

            migrationBuilder.DropTable(
                name: "goal_contributions");

            migrationBuilder.DropTable(
                name: "incomes");

            migrationBuilder.DropTable(
                name: "join_requests");

            migrationBuilder.DropTable(
                name: "notification_subscriptions");

            migrationBuilder.DropTable(
                name: "one_off_expenses");

            migrationBuilder.DropTable(
                name: "period_recurring_items");

            migrationBuilder.DropTable(
                name: "rate_overrides");

            migrationBuilder.DropTable(
                name: "savings_entries");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "webhook_endpoints");

            migrationBuilder.DropTable(
                name: "debts");

            migrationBuilder.DropTable(
                name: "goals");

            migrationBuilder.DropTable(
                name: "recurring_expenses");

            migrationBuilder.DropTable(
                name: "budget_periods");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "budget_contexts");
        }
    }
}
