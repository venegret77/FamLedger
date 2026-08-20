using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class OneOffExpenseService(
    AppDbContext db,
    IContextService contextService,
    IExchangeRateService exchangeRateService) : IOneOffExpenseService
{
    public async Task<OneOffExpense> CreateAsync(Guid contextId, Guid periodId, Guid userId, string name, decimal amount, string currency, CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var period = await db.BudgetPeriods.FindAsync([periodId], ct) ?? throw new InvalidOperationException();
        var baseAmount = currency.Equals("RSD", StringComparison.OrdinalIgnoreCase)
            ? amount
            : await exchangeRateService.ConvertToBaseAsync(amount, currency, period.StartDate, contextId, periodId, ct);

        var expense = new OneOffExpense
        {
            ContextId = contextId,
            PeriodId = periodId,
            Name = name,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            BaseAmount = baseAmount
        };
        db.OneOffExpenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return expense;
    }

    public Task<IReadOnlyList<OneOffExpense>> GetByPeriodAsync(Guid periodId, CancellationToken ct = default) =>
        db.OneOffExpenses.Where(o => o.PeriodId == periodId).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<OneOffExpense>)t.Result, ct);

    public async Task TogglePaidAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var expense = await db.OneOffExpenses.FindAsync([id], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(expense.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();
        expense.IsPaid = !expense.IsPaid;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var expense = await db.OneOffExpenses.FindAsync([id], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(expense.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();
        db.OneOffExpenses.Remove(expense);
        await db.SaveChangesAsync(ct);
    }
}
