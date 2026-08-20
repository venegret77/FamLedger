using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class SavingsService(AppDbContext db, IContextService contextService) : ISavingsService
{
    public async Task<SavingsEntry> GetOrCreateForPeriodAsync(Guid contextId, Guid periodId, CancellationToken ct = default)
    {
        var entry = await db.SavingsEntries.FirstOrDefaultAsync(s => s.ContextId == contextId && s.PeriodId == periodId, ct);
        if (entry is not null) return entry;

        entry = new SavingsEntry { ContextId = contextId, PeriodId = periodId };
        db.SavingsEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task AddDepositAsync(Guid contextId, Guid periodId, decimal amount, Guid userId, CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var entry = await GetOrCreateForPeriodAsync(contextId, periodId, ct);
        entry.ActualAmount += amount;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetPlanAsync(Guid contextId, Guid periodId, decimal plannedAmount, Guid userId, CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var entry = await GetOrCreateForPeriodAsync(contextId, periodId, ct);
        entry.PlannedAmount = plannedAmount;
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
}
