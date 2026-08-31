using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Domain.Models;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class ReconciliationService(
    AppDbContext db,
    IContextService contextService,
    IBudgetCalculatorService calculator,
    IDebtService debtService,
    IRecurringExpenseService recurringService,
    IOneOffExpenseService oneOffService,
    IExchangeRateService exchangeRateService) : IReconciliationService
{
    public async Task<ReconciliationView> GetAsync(
        Guid contextId,
        Guid periodId,
        Guid userId,
        CancellationToken ct = default)
    {
        var (context, period, canEdit) = await GetContextAsync(contextId, periodId, userId, ct);
        var manual = await LoadManualAsync(context.Id, period.Id, ct);
        return await BuildViewAsync(context, period, manual, canEdit, ct);
    }

    public async Task<ReconciliationView> SaveManualAsync(
        Guid contextId,
        Guid periodId,
        Guid userId,
        ReconciliationManualInput manual,
        CancellationToken ct = default)
    {
        var (context, period, canEdit) = await GetContextAsync(contextId, periodId, userId, ct);
        if (!canEdit)
            throw new UnauthorizedAccessException();

        var entity = await db.PeriodReconciliations
            .FirstOrDefaultAsync(r => r.ContextId == context.Id && r.PeriodId == period.Id, ct);

        if (entity is null)
        {
            entity = new PeriodReconciliation
            {
                ContextId = context.Id,
                PeriodId = period.Id
            };
            db.PeriodReconciliations.Add(entity);
        }

        entity.AssetItemsJson = ReconciliationItemsHelper.ToJson(manual.AssetItems);
        entity.ObligationItemsJson = ReconciliationItemsHelper.ToJson(manual.ObligationItems);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);

        return await BuildViewAsync(context, period, manual, canEdit, ct);
    }

    private async Task<(BudgetContext Context, BudgetPeriod Period, bool CanEdit)> GetContextAsync(
        Guid contextId,
        Guid periodId,
        Guid userId,
        CancellationToken ct)
    {
        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        var period = await db.BudgetPeriods.FirstOrDefaultAsync(
            p => p.Id == periodId && p.ContextId == contextId, ct)
            ?? throw new InvalidOperationException("Period not found.");

        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        var canEdit = member is not null && RolePermissions.CanManagePlan(member.Role);
        return (context, period, canEdit);
    }

    private async Task<ReconciliationManualInput> LoadManualAsync(
        Guid contextId,
        Guid periodId,
        CancellationToken ct)
    {
        var entity = await db.PeriodReconciliations
            .FirstOrDefaultAsync(r => r.ContextId == contextId && r.PeriodId == periodId, ct);

        if (entity is null)
            return EmptyManual();

        return new ReconciliationManualInput(
            ReconciliationItemsHelper.ParseItems(entity.AssetItemsJson),
            ReconciliationItemsHelper.ParseItems(entity.ObligationItemsJson));
    }

    private async Task<ReconciliationView> BuildViewAsync(
        BudgetContext context,
        BudgetPeriod period,
        ReconciliationManualInput manual,
        bool canEdit,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await calculator.CalculateAsync(context, period, today, ct);

        var savingsActual = await GetSavingsActualByCurrencyAsync(context.Id, ct);
        var debtsOwedToUs = await GetDebtsByCurrencyAsync(context.Id, DebtDirection.TheyOwe, ct);
        var debtsWeOwe = await GetDebtsByCurrencyAsync(context.Id, DebtDirection.WeOwe, ct);
        var unpaidPlanned = await GetUnpaidPlannedByCurrencyAsync(period.Id, ct);

        var manualAssets = ReconciliationItemsHelper.ToCurrencyTotals(manual.AssetItems);
        var manualObligations = ReconciliationItemsHelper.ToCurrencyTotals(manual.ObligationItems);

        var assetLines = new List<ReconciliationLine>
        {
            AutoLine("savingsActual", "Копилка (факт)", savingsActual),
            AutoLine("debtsOwedToUs", "Долги (нам должны)", debtsOwedToUs),
        };
        assetLines.AddRange(manual.AssetItems.Select(ManualEntryLine));

        var obligationLines = new List<ReconciliationLine>
        {
            AutoLine("unpaidPlanned", "Плановые расходы (не оплачены)", unpaidPlanned),
            AutoLine("debtsWeOwe", "Долги (мы должны)", debtsWeOwe),
        };
        obligationLines.AddRange(manual.ObligationItems.Select(ManualEntryLine));

        var assetTotals = CurrencyAmountHelper.MergeAmounts(
            savingsActual,
            manualAssets,
            debtsOwedToUs);
        var obligationTotals = CurrencyAmountHelper.MergeAmounts(
            unpaidPlanned,
            manualObligations,
            debtsWeOwe);

        var assetTotalBase = await SumToBaseAsync(context, period.Id, assetTotals, ct);
        var obligationTotalBase = await SumToBaseAsync(context, period.Id, obligationTotals, ct);
        var actualNet = assetTotalBase - obligationTotalBase;

        var ledgerIncome = summary.Income + summary.TopUps;
        var ledgerExpenses = summary.PlannedExpenses + summary.Spent;
        var ledgerTotal = summary.Remaining;

        return new ReconciliationView(
            period.Id,
            period.Label,
            context.BaseCurrency,
            canEdit,
            new ReconciliationSide(
                assetLines,
                CurrencyAmountHelper.ToAmounts(assetTotals),
                assetTotalBase),
            new ReconciliationSide(
                obligationLines,
                CurrencyAmountHelper.ToAmounts(obligationTotals),
                obligationTotalBase),
            new ReconciliationSummary(
                ledgerIncome,
                ledgerExpenses,
                ledgerTotal,
                actualNet,
                ledgerTotal - actualNet),
            manual);
    }

    private static ReconciliationLine AutoLine(
        string key,
        string label,
        IReadOnlyDictionary<string, decimal> amounts) =>
        new(key, label, false, CurrencyAmountHelper.ToAmounts(amounts));

    private static ReconciliationLine ManualEntryLine(ReconciliationManualEntry entry) =>
        new(
            $"item-{entry.Id}",
            entry.Name,
            true,
            [new CurrencyAmount(entry.Currency, entry.Amount)],
            entry.Id);

    private static ReconciliationManualInput EmptyManual() =>
        new([], []);

    private async Task<Dictionary<string, decimal>> GetSavingsActualByCurrencyAsync(
        Guid contextId,
        CancellationToken ct)
    {
        var deposits = await db.SavingsDeposits
            .Where(d => d.ContextId == contextId)
            .Select(d => new { d.Amount, d.Currency })
            .ToListAsync(ct);

        return deposits
            .GroupBy(d => d.Currency.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, decimal>> GetDebtsByCurrencyAsync(
        Guid contextId,
        DebtDirection direction,
        CancellationToken ct)
    {
        var debts = await debtService.GetByContextAsync(contextId, hidePaid: true, ct);
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var debt in debts.Where(d => d.Direction == direction))
        {
            foreach (var entry in debt.Entries.Where(e => !e.IsPaid))
            {
                var code = entry.Currency.ToUpperInvariant();
                result[code] = result.GetValueOrDefault(code) + entry.Amount;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, decimal>> GetUnpaidPlannedByCurrencyAsync(
        Guid periodId,
        CancellationToken ct)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var recurring = await recurringService.GetPeriodItemsAsync(periodId, ct);
        foreach (var item in recurring.Where(i => !i.IsSkipped && !i.IsPaid))
        {
            var currency = item.RecurringExpense.DefinitionCurrency.ToUpperInvariant();
            result[currency] = result.GetValueOrDefault(currency) + item.RecurringExpense.DefinitionAmount;
        }

        var oneOff = await oneOffService.GetByPeriodAsync(periodId, ct);
        foreach (var item in oneOff.Where(i => !i.IsPaid))
        {
            var currency = item.Currency.ToUpperInvariant();
            result[currency] = result.GetValueOrDefault(currency) + item.Amount;
        }

        return result;
    }

    private async Task<decimal> SumToBaseAsync(
        BudgetContext context,
        Guid periodId,
        IReadOnlyDictionary<string, decimal> amounts,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        decimal total = 0;
        foreach (var (currency, amount) in amounts)
        {
            if (amount == 0) continue;
            total += currency.Equals(context.BaseCurrency, StringComparison.OrdinalIgnoreCase)
                ? amount
                : await exchangeRateService.ConvertToBaseAsync(
                    amount, currency, today, context.Id, periodId, ct);
        }

        return total;
    }
}
