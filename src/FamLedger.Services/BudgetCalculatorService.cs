using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Domain.Models;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class BudgetCalculatorService(
    AppDbContext db,
    IExchangeRateService exchangeRateService) : IBudgetCalculatorService
{
    public async Task<BudgetSummary> CalculateAsync(BudgetContext context, BudgetPeriod period, DateOnly today, CancellationToken ct = default)
    {
        var daysInPeriod = BudgetPeriodMath.GetDaysInPeriod(period.StartDate, period.EndDate);
        var daysPassed = today < period.StartDate
            ? 0
            : today > period.EndDate
                ? daysInPeriod
                : BudgetPeriodMath.GetDaysPassed(period.StartDate, today);
        // Дни с сегодня по конец периода включительно.
        var daysRemaining = today < period.StartDate
            ? daysInPeriod
            : today > period.EndDate
                ? 0
                : BudgetPeriodMath.GetDaysInPeriod(today, period.EndDate);

        var incomeTotal = await SumIncomesInBaseAsync(context, period, ct);

        var recurringTotal = await db.PeriodRecurringItems
            .Where(x => x.PeriodId == period.Id && !x.IsSkipped)
            .SumAsync(x => x.PlannedBaseAmount, ct);

        var oneOffTotal = await db.OneOffExpenses
            .Where(x => x.PeriodId == period.Id)
            .SumAsync(x => x.BaseAmount, ct);

        var plannedExpenses = recurringTotal + oneOffTotal;
        var spent = await db.Transactions
            .Where(t => t.PeriodId == period.Id && t.Kind == TransactionKind.Expense)
            .SumAsync(t => t.BaseAmount, ct);
        var topUps = await db.Transactions
            .Where(t => t.PeriodId == period.Id && t.Kind == TransactionKind.Income)
            .SumAsync(t => t.BaseAmount, ct);
        var spentToday = await db.Transactions
            .Where(t => t.PeriodId == period.Id && t.Kind == TransactionKind.Expense && t.Date == today)
            .SumAsync(t => t.BaseAmount, ct);
        var spentBeforeToday = spent - spentToday;

        // Копилка не участвует. Остаток = плановые доходы + пополнения − план − факт.
        var remaining = incomeTotal + topUps - plannedExpenses - spent;
        var envelope = incomeTotal + topUps - plannedExpenses;
        var dailyBudget = daysInPeriod > 0 ? envelope / daysInPeriod : 0m;

        // 15–20 включительно = 6 дней. Доступно сегодня: 6 × дневной − все расходы периода.
        var availableToday = daysPassed * dailyBudget - spent;
        var daysBeforeToday = Math.Max(daysPassed - (today >= period.StartDate && today <= period.EndDate ? 1 : 0), 0);
        var carryover = daysBeforeToday * dailyBudget - spentBeforeToday;

        return new BudgetSummary
        {
            PeriodId = period.Id,
            Income = incomeTotal,
            TopUps = topUps,
            PlannedExpenses = plannedExpenses,
            Spent = spent,
            Carryover = carryover,
            Remaining = remaining,
            DailyBudgetAtStart = dailyBudget,
            DailyBudgetNow = dailyBudget,
            AvailableToday = availableToday,
            SpentToday = spentToday,
            DaysInPeriod = daysInPeriod,
            DaysPassed = daysPassed,
            DaysRemaining = daysRemaining,
            PeriodLabel = period.Label
        };
    }

    private async Task<decimal> SumIncomesInBaseAsync(BudgetContext context, BudgetPeriod period, CancellationToken ct)
    {
        var incomes = await db.Incomes.Where(i => i.ContextId == context.Id).ToListAsync(ct);
        var total = 0m;
        foreach (var income in incomes)
        {
            total += IsBaseCurrency(income.Currency, context.BaseCurrency)
                ? income.Amount
                : await exchangeRateService.ConvertToBaseAsync(
                    income.Amount, income.Currency, period.StartDate, context.Id, period.Id, ct);
        }
        return total;
    }

    private static bool IsBaseCurrency(string currency, string baseCurrency) =>
        currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase);
}
