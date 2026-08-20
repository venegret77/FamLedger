using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class SavingsService(
    AppDbContext db,
    IContextService contextService,
    IExchangeRateService exchangeRateService) : ISavingsService
{
    public async Task<SavingsEntry> GetOrCreateForPeriodAsync(Guid contextId, Guid periodId, CancellationToken ct = default)
    {
        var entry = await db.SavingsEntries.FirstOrDefaultAsync(s => s.ContextId == contextId && s.PeriodId == periodId, ct);
        if (entry is not null) return entry;

        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        entry = new SavingsEntry
        {
            ContextId = contextId,
            PeriodId = periodId,
            Currency = context.BaseCurrency
        };
        db.SavingsEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task AddDepositAsync(
        Guid contextId,
        Guid periodId,
        decimal amount,
        string currency,
        Guid userId,
        CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var baseAmount = await ToBaseAmountAsync(contextId, periodId, amount, currency, ct);
        var entry = await GetOrCreateForPeriodAsync(contextId, periodId, ct);
        entry.ActualAmount += baseAmount;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetPlanAsync(
        Guid contextId,
        Guid periodId,
        decimal plannedAmount,
        string currency,
        Guid userId,
        CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var baseAmount = await ToBaseAmountAsync(contextId, periodId, plannedAmount, currency, ct);
        var entry = await GetOrCreateForPeriodAsync(contextId, periodId, ct);
        entry.PlannedAmount = baseAmount;
        await db.SaveChangesAsync(ct);
    }

    public Task<decimal> GetTotalBalanceAsync(Guid contextId, CancellationToken ct = default) =>
        db.SavingsEntries.Where(s => s.ContextId == contextId).SumAsync(s => s.ActualAmount, ct);

    public Task<IReadOnlyList<SavingsEntry>> GetPlansAsync(Guid contextId, CancellationToken ct = default) =>
        db.SavingsEntries
            .Include(s => s.Period)
            .Where(s => s.ContextId == contextId)
            .OrderBy(s => s.Period!.StartDate)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<SavingsEntry>)t.Result, ct);

    private async Task<decimal> ToBaseAmountAsync(
        Guid contextId,
        Guid periodId,
        decimal amount,
        string currency,
        CancellationToken ct)
    {
        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        var period = await db.BudgetPeriods.FindAsync([periodId], ct)
            ?? throw new InvalidOperationException("Period not found.");
        var code = string.IsNullOrWhiteSpace(currency)
            ? context.BaseCurrency
            : currency.ToUpperInvariant();

        if (code.Equals(context.BaseCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;

        return await exchangeRateService.ConvertToBaseAsync(
            amount, code, period.StartDate, contextId, periodId, ct);
    }
}
