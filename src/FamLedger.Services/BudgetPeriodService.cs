using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class BudgetPeriodService(
    AppDbContext db,
    IExchangeRateService exchangeRateService) : IBudgetPeriodService
{
    public async Task<BudgetPeriod> EnsureActivePeriodAsync(BudgetContext context, CancellationToken ct = default)
    {
        var active = await db.BudgetPeriods
            .FirstOrDefaultAsync(p => p.ContextId == context.Id && !p.IsClosed, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (active is not null && today >= active.StartDate && today <= active.EndDate)
            return active;

        if (active is not null && today > active.EndDate)
            return await ClosePeriodAsync(active, context, ct);

        var (start, end, label) = GetPeriodBounds(context, today);
        var period = new BudgetPeriod { ContextId = context.Id, Label = label, StartDate = start, EndDate = end };
        db.BudgetPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        await CopyRecurringItemsAsync(context, period, ct);
        return period;
    }

    public async Task<BudgetPeriod> ClosePeriodAsync(BudgetPeriod period, BudgetContext context, CancellationToken ct = default)
    {
        period.IsClosed = true;

        var nextStart = period.EndDate.AddDays(1);
        var (start, end, label) = GetPeriodBounds(context, nextStart);
        var newPeriod = new BudgetPeriod
        {
            ContextId = context.Id,
            Label = label,
            StartDate = start,
            EndDate = end,
            CarryoverBase = 0
        };
        db.BudgetPeriods.Add(newPeriod);
        await db.SaveChangesAsync(ct);
        await CopyRecurringItemsAsync(context, newPeriod, ct);
        return newPeriod;
    }

    public (DateOnly Start, DateOnly End, string Label) GetPeriodBounds(BudgetContext context, DateOnly referenceDate)
    {
        var startDay = Math.Clamp(context.PeriodStartDay, 1, 28);
        var startMonth = referenceDate.Day >= startDay
            ? new DateOnly(referenceDate.Year, referenceDate.Month, startDay)
            : new DateOnly(referenceDate.Year, referenceDate.Month, 1).AddMonths(-1).AddDays(startDay - 1);
        var end = startMonth.AddMonths(1).AddDays(-1);
        return (startMonth, end, PeriodLabelHelper.GetPeriodLabel(startMonth));
    }

    private async Task CopyRecurringItemsAsync(BudgetContext context, BudgetPeriod period, CancellationToken ct)
    {
        var recurring = await db.RecurringExpenses.Where(r => r.ContextId == context.Id).ToListAsync(ct);
        foreach (var expense in recurring)
        {
            var baseAmount = expense.DefinitionCurrency.Equals("RSD", StringComparison.OrdinalIgnoreCase)
                ? expense.DefinitionAmount
                : await exchangeRateService.ConvertToBaseAsync(expense.DefinitionAmount, expense.DefinitionCurrency, period.StartDate, context.Id, period.Id, ct);

            db.PeriodRecurringItems.Add(new PeriodRecurringItem
            {
                PeriodId = period.Id,
                RecurringExpenseId = expense.Id,
                PlannedBaseAmount = baseAmount
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
