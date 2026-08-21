using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    /// <inheritdoc />
    public partial class TransactionKindAndReminderKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reminders_ContextId",
                table: "reminders");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "TimeUtc",
                table: "reminders",
                type: "time without time zone",
                nullable: true,
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "reminders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ThresholdPercent",
                table: "reminders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminders_ContextId_Kind_CreatedByUserId",
                table: "reminders",
                columns: new[] { "ContextId", "Kind", "CreatedByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reminders_ContextId_Kind_CreatedByUserId",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "ThresholdPercent",
                table: "reminders");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "TimeUtc",
                table: "reminders",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0),
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "reminders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminders_ContextId",
                table: "reminders",
                column: "ContextId");
        }
    }
}
