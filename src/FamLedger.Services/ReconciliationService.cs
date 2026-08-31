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
    ISavingsService savingsService,
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
        var manual = await GetOrCreateManualAsync(context.Id, period.Id, ct);
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

        entity.CardsJson = CurrencyAmountHelper.ToJson(manual.Cards);
        entity.CashJson = CurrencyAmountHelper.ToJson(manual.Cash);
        entity.SetAsideJson = CurrencyAmountHelper.ToJson(manual.SetAside);
        entity.ManualPlannedJson = CurrencyAmountHelper.ToJson(manual.ManualPlanned);
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

    private async Task<ReconciliationManualInput> GetOrCreateManualAsync(
        Guid contextId,
        Guid periodId,
        CancellationToken ct)
    {
        var entity = await db.PeriodReconciliations
            .FirstOrDefaultAsync(r => r.ContextId == contextId && r.PeriodId == periodId, ct);

        if (entity is null)
            return EmptyManual();

        return new ReconciliationManualInput(
            CurrencyAmountHelper.ParseJson(entity.CardsJson),
            CurrencyAmountHelper.ParseJson(entity.CashJson),
            CurrencyAmountHelper.ParseJson(entity.SetAsideJson),
            CurrencyAmountHelper.ParseJson(entity.ManualPlannedJson));
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
        var savingsPlan = await GetSavingsPlanByCurrencyAsync(context.Id, period.Id, ct);
        var debtsOwedToUs = await GetDebtsByCurrencyAsync(context.Id, DebtDirection.TheyOwe, ct);
        var debtsWeOwe = await GetDebtsByCurrencyAsync(context.Id, DebtDirection.WeOwe, ct);
        var unpaidPlanned = await GetUnpaidPlannedByCurrencyAsync(period.Id, ct);

        var assetLines = new List<ReconciliationLine>
        {
            Line("savingsActual", "Копилка (факт)", false, savingsActual),
            Line("cards", "Карты", true, manual.Cards),
            Line("cash", "Наличные", true, manual.Cash),
            Line("setAside", "Отложено на след. месяц", true, manual.SetAside),
            Line("debtsOwedToUs", "Долги (нам должны)", false, debtsOwedToUs)
        };

        var obligationLines = new List<ReconciliationLine>
        {
            Line("savingsPlan", "Копилка (план)", false, savingsPlan),
            Line("unpaidPlanned", "Плановые расходы (не оплачены)", false, unpaidPlanned),
            Line("manualPlanned", "Плановые вручную", true, manual.ManualPlanned),
            Line("debtsWeOwe", "Долги (мы должны)", false, debtsWeOwe)
        };

        var assetTotals = CurrencyAmountHelper.MergeAmounts(
            savingsActual,
            manual.Cards,
            manual.Cash,
            manual.SetAside,
            debtsOwedToUs);
        var obligationTotals = CurrencyAmountHelper.MergeAmounts(
            savingsPlan,
            unpaidPlanned,
            manual.ManualPlanned,
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

    private static ReconciliationLine Line(
        string key,
        string label,
        bool isManual,
        IReadOnlyDictionary<string, decimal> amounts) =>
        new(key, label, isManual, CurrencyAmountHelper.ToAmounts(amounts));

    private static ReconciliationManualInput EmptyManual() =>
        new(
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>());

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

    private async Task<Dictionary<string, decimal>> GetSavingsPlanByCurrencyAsync(
        Guid contextId,
        Guid periodId,
        CancellationToken ct)
    {
        var entry = await savingsService.GetOrCreateForPeriodAsync(contextId, periodId, ct);
        if (entry.PlannedAmount == 0)
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var currency = string.IsNullOrWhiteSpace(entry.PlannedCurrency)
            ? entry.Currency
            : entry.PlannedCurrency;
        return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [currency.ToUpperInvariant()] = entry.PlannedAmount
        };
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
