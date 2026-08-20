using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class IncomeService(AppDbContext db, IContextService contextService) : IIncomeService
{
    public async Task<Income> CreateAsync(Guid contextId, Guid userId, string name, decimal amount, string currency, CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var income = new Income { ContextId = contextId, Name = name, Amount = amount, Currency = currency.ToUpperInvariant() };
        db.Incomes.Add(income);
        await db.SaveChangesAsync(ct);
        return income;
    }

    public Task<IReadOnlyList<Income>> GetByContextAsync(Guid contextId, CancellationToken ct = default) =>
        db.Incomes.Where(i => i.ContextId == contextId).OrderBy(i => i.SortOrder).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Income>)t.Result, ct);

    public async Task UpdateAsync(Guid id, Guid userId, string name, decimal amount, string currency, CancellationToken ct = default)
    {
        var income = await db.Incomes.FindAsync([id], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(income.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();
        income.Name = name;
        income.Amount = amount;
        income.Currency = currency.ToUpperInvariant();
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var income = await db.Incomes.FindAsync([id], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(income.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();
        db.Incomes.Remove(income);
        await db.SaveChangesAsync(ct);
    }
}
