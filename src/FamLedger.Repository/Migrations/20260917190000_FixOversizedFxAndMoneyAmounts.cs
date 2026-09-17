using FamLedger.Repository;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260917190000_FixOversizedFxAndMoneyAmounts")]
    public partial class FixOversizedFxAndMoneyAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Unbounded PostgreSQL numeric can hold values System.Decimal cannot read.
            // Comparisons stay in SQL; never SELECT oversized values into the app.
            migrationBuilder.Sql(
                """
                DELETE FROM exchange_rates
                WHERE "RateToRsd" <= 0 OR "RateToRsd" > 10000;

                DELETE FROM rate_overrides
                WHERE "RateToRsd" <= 0 OR "RateToRsd" > 10000;

                -- High-scale numerics (e.g. GEL cross-rate) overflow System.Decimal on SUM.
                UPDATE exchange_rates SET "RateToRsd" = ROUND("RateToRsd", 6);
                UPDATE rate_overrides SET "RateToRsd" = ROUND("RateToRsd", 6);

                UPDATE transactions
                SET "BaseAmount" = ROUND("Amount" * 38.5, 4)
                WHERE UPPER("Currency") = 'GEL'
                  AND "Amount" > 0
                  AND (
                    "BaseAmount" <= 0
                    OR "BaseAmount" / "Amount" > 1000
                    OR "BaseAmount" / "Amount" < 1
                  );

                UPDATE transactions
                SET "BaseAmount" = ROUND("BaseAmount", 4),
                    "Amount" = ROUND("Amount", 4);

                UPDATE transactions
                SET "BaseAmount" = "Amount"
                WHERE "BaseAmount" > 1000000000000 OR "BaseAmount" < -1000000000000;

                UPDATE one_off_expenses
                SET "BaseAmount" = ROUND("BaseAmount", 4),
                    "Amount" = ROUND("Amount", 4);

                UPDATE one_off_expenses
                SET "BaseAmount" = "Amount"
                WHERE "BaseAmount" > 1000000000000 OR "BaseAmount" < -1000000000000;

                UPDATE period_recurring_items
                SET "PlannedBaseAmount" = ROUND("PlannedBaseAmount", 4);

                UPDATE period_recurring_items pri
                SET "PlannedBaseAmount" = re."DefinitionAmount"
                FROM recurring_expenses re
                WHERE pri."RecurringExpenseId" = re."Id"
                  AND UPPER(re."DefinitionCurrency") = 'RSD'
                  AND (pri."PlannedBaseAmount" > 1000000000000 OR pri."PlannedBaseAmount" < 0);

                UPDATE period_recurring_items
                SET "PlannedBaseAmount" = 0
                WHERE "PlannedBaseAmount" > 1000000000000 OR "PlannedBaseAmount" < 0;

                INSERT INTO exchange_rates ("Id", "Date", "Currency", "RateToRsd", "FetchedAt")
                SELECT gen_random_uuid(), CURRENT_DATE, 'GEL', 38.5, NOW()
                WHERE NOT EXISTS (
                    SELECT 1 FROM exchange_rates
                    WHERE "Currency" = 'GEL' AND "Date" = CURRENT_DATE
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data repair is not reversible.
        }
    }
}
