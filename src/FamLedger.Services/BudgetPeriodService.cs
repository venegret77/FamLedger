using System.Text.Json;
using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Domain.Models;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class BudgetPeriodService(
    AppDbContext db,
    IExchangeRateService exchangeRateService,
    IBudgetCalculatorService calculator,
    IContextService contextService) : IBudgetPeriodService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<BudgetPeriod> EnsureActivePeriodAsync(BudgetContext context, CancellationToken ct = default)
    {
        var active = await db.BudgetPeriods
            .FirstOrDefaultAsync(p => p.ContextId == context.Id && !p.IsClosed, ct);

        // Keep overdue periods open until the user starts a new month manually.
        if (active is not null)
            return active;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (start, end, label) = GetPeriodBounds(context, today);
        var period = new BudgetPeriod { ContextId = context.Id, Label = label, StartDate = start, EndDate = end };
        db.BudgetPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        await CopyRecurringItemsAsync(context, period, ct);
        return period;
    }

    public async Task<BudgetPeriod> CloseActivePeriodAsync(Guid contextId, Guid userId, CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");

        var period = await db.BudgetPeriods
            .FirstOrDefaultAsync(p => p.ContextId == contextId && !p.IsClosed, ct)
            ?? throw new InvalidOperationException("Нет активного периода.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!PeriodCloseRules.CanStartNewPeriod(period, today))
            throw new InvalidOperationException(
                $"Новый месяц можно начать за {PeriodCloseRules.WindowDays} дн. до конца периода или после его окончания.");

        return await ClosePeriodAsync(period, context, userId, ct);
    }

    public async Task<BudgetPeriod> ClosePeriodAsync(
        BudgetPeriod period,
        BudgetContext context,
        Guid? closedByUserId = null,
        CancellationToken ct = default)
    {
        if (period.IsClosed)
            throw new InvalidOperationException("Период уже закрыт.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await calculator.CalculateAsync(context, period, today, ct);
        var snapshot = await BuildSnapshotAsync(period, context, summary, closedByUserId, ct);
        db.PeriodSnapshots.Add(snapshot);

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

    public async Task<IReadOnlyList<PeriodListItem>> GetPeriodsAsync(Guid contextId, CancellationToken ct = default)
    {
        var periods = await db.BudgetPeriods
            .AsNoTracking()
            .Include(p => p.Snapshot)
            .Where(p => p.ContextId == contextId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(ct);

        return periods.Select(p =>
        {
            var snap = p.Snapshot;
            return new PeriodListItem(
                p.Id,
                p.Label,
                p.StartDate,
                p.EndDate,
                p.IsClosed,
                !p.IsClosed,
                snap?.Income,
                snap?.TopUps,
                snap?.PlannedExpenses,
                snap?.Spent,
                snap?.Remaining,
                snap?.TransactionCount,
                snap?.ClosedAt);
        }).ToList();
    }

    public async Task<PeriodHistoryDetail?> GetPeriodHistoryAsync(Guid contextId, Guid periodId, CancellationToken ct = default)
    {
        var period = await db.BudgetPeriods
            .AsNoTracking()
            .Include(p => p.Snapshot)
            .FirstOrDefaultAsync(p => p.Id == periodId && p.ContextId == contextId, ct);
        if (period is null)
            return null;

        if (period.Snapshot is not null)
            return FromSnapshot(period, period.Snapshot);

        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await calculator.CalculateAsync(context, period, today, ct);
        var (byCategory, byDay, expenseCount, incomeCount, txCount) =
            await AggregateTransactionsAsync(period.Id, ct);

        return new PeriodHistoryDetail(
            period.Id,
            period.Label,
            period.StartDate,
            period.EndDate,
            period.IsClosed,
            !period.IsClosed,
            summary.Income,
            summary.TopUps,
            summary.PlannedExpenses,
            summary.Spent,
            summary.Remaining,
            summary.DailyBudgetAtStart,
            summary.DaysInPeriod,
            txCount,
            expenseCount,
            incomeCount,
            null,
            byCategory,
            byDay);
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

    private async Task<PeriodSnapshot> BuildSnapshotAsync(
        BudgetPeriod period,
        BudgetContext context,
        BudgetSummary summary,
        Guid? closedByUserId,
        CancellationToken ct)
    {
        var (byCategory, byDay, expenseCount, incomeCount, txCount) =
            await AggregateTransactionsAsync(period.Id, ct);

        return new PeriodSnapshot
        {
            PeriodId = period.Id,
            ContextId = context.Id,
            ClosedByUserId = closedByUserId,
            ClosedAt = DateTime.UtcNow,
            Income = summary.Income,
            TopUps = summary.TopUps,
            PlannedExpenses = summary.PlannedExpenses,
            Spent = summary.Spent,
            Remaining = summary.Remaining,
            DailyBudget = summary.DailyBudgetAtStart,
            DaysInPeriod = summary.DaysInPeriod,
            TransactionCount = txCount,
            ExpenseCount = expenseCount,
            IncomeCount = incomeCount,
            CategoryBreakdownJson = JsonSerializer.Serialize(byCategory, JsonOptions),
            DailyBreakdownJson = JsonSerializer.Serialize(
                byDay.Select(d => new { date = d.Date.ToString("yyyy-MM-dd"), spent = d.Spent, topUps = d.TopUps }),
                JsonOptions)
        };
    }

    private async Task<(
        IReadOnlyList<CategoryBreakdownItem> ByCategory,
        IReadOnlyList<DailyBreakdownItem> ByDay,
        int ExpenseCount,
        int IncomeCount,
        int TransactionCount)> AggregateTransactionsAsync(Guid periodId, CancellationToken ct)
    {
        var transactions = await db.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.PeriodId == periodId)
            .ToListAsync(ct);

        var byCategory = transactions
            .Where(t => t.Kind == TransactionKind.Expense)
            .GroupBy(t => t.Category?.Name ?? "Без категории")
            .Select(g => new CategoryBreakdownItem(g.Key, g.Sum(x => x.BaseAmount), g.Count()))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var byDay = transactions
            .GroupBy(t => t.Date)
            .Select(g => new DailyBreakdownItem(
                g.Key,
                g.Where(t => t.Kind == TransactionKind.Expense).Sum(t => t.BaseAmount),
                g.Where(t => t.Kind == TransactionKind.Income).Sum(t => t.BaseAmount)))
            .OrderByDescending(x => x.Date)
            .ToList();

        var expenseCount = transactions.Count(t => t.Kind == TransactionKind.Expense);
        var incomeCount = transactions.Count(t => t.Kind == TransactionKind.Income);
        return (byCategory, byDay, expenseCount, incomeCount, transactions.Count);
    }

    private static PeriodHistoryDetail FromSnapshot(BudgetPeriod period, PeriodSnapshot snap)
    {
        var byCategory = DeserializeList<CategoryBreakdownItem>(snap.CategoryBreakdownJson);
        var byDayRaw = DeserializeList<DailyBreakdownDto>(snap.DailyBreakdownJson);
        var byDay = byDayRaw
            .Select(d => new DailyBreakdownItem(
                DateOnly.TryParse(d.Date, out var date) ? date : period.StartDate,
                d.Spent,
                d.TopUps))
            .ToList();

        return new PeriodHistoryDetail(
            period.Id,
            period.Label,
            period.StartDate,
            period.EndDate,
            period.IsClosed,
            !period.IsClosed,
            snap.Income,
            snap.TopUps,
            snap.PlannedExpenses,
            snap.Spent,
            snap.Remaining,
            snap.DailyBudget,
            snap.DaysInPeriod,
            snap.TransactionCount,
            snap.ExpenseCount,
            snap.IncomeCount,
            snap.ClosedAt,
            byCategory,
            byDay);
    }

    private static List<T> DeserializeList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed class DailyBreakdownDto
    {
        public string Date { get; set; } = string.Empty;
        public decimal Spent { get; set; }
        public decimal TopUps { get; set; }
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
