using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class RecurringExpenseService(
    AppDbContext db,
    IContextService contextService,
    IBudgetPeriodService periodService,
    IExchangeRateService exchangeRateService) : IRecurringExpenseService
{
    public async Task<RecurringExpense> CreateAsync(Guid contextId, Guid userId, string name, decimal amount, string currency, int chargeDay, CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var expense = new RecurringExpense
        {
            ContextId = contextId,
            Name = name,
            DefinitionAmount = amount,
            DefinitionCurrency = currency.ToUpperInvariant(),
            ChargeDayOfMonth = Math.Clamp(chargeDay, 1, 28)
        };
        db.RecurringExpenses.Add(expense);

        var context = await db.BudgetContexts.FindAsync([contextId], ct)!;
        var period = await periodService.EnsureActivePeriodAsync(context!, ct);
        var baseAmount = currency.Equals("RSD", StringComparison.OrdinalIgnoreCase)
            ? amount
            : await exchangeRateService.ConvertToBaseAsync(amount, currency, period.StartDate, contextId, period.Id, ct);

        db.PeriodRecurringItems.Add(new PeriodRecurringItem
        {
            PeriodId = period.Id,
            RecurringExpenseId = expense.Id,
            PlannedBaseAmount = baseAmount
        });
        await db.SaveChangesAsync(ct);
        return expense;
    }

    public Task<IReadOnlyList<PeriodRecurringItem>> GetPeriodItemsAsync(Guid periodId, CancellationToken ct = default) =>
        db.PeriodRecurringItems
            .Include(i => i.RecurringExpense)
            .Where(i => i.PeriodId == periodId)
            .OrderBy(i => i.RecurringExpense.ChargeDayOfMonth)
            .ThenBy(i => i.RecurringExpense.Name)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<PeriodRecurringItem>)t.Result, ct);

    public async Task TogglePaidAsync(Guid itemId, Guid userId, CancellationToken ct = default)
    {
        var item = await db.PeriodRecurringItems
            .Include(i => i.Period)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct) ?? throw new InvalidOperationException();

        var member = await contextService.GetMembershipAsync(item.Period.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        item.IsPaid = !item.IsPaid;
        item.PaidAt = item.IsPaid ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(ct);
    }

    public async Task ToggleSkippedAsync(Guid itemId, Guid userId, CancellationToken ct = default)
    {
        var item = await db.PeriodRecurringItems
            .Include(i => i.Period)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct) ?? throw new InvalidOperationException();

        var member = await contextService.GetMembershipAsync(item.Period.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        item.IsSkipped = !item.IsSkipped;
        if (item.IsSkipped)
        {
            item.IsPaid = false;
            item.PaidAt = null;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var expense = await db.RecurringExpenses.FindAsync([id], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(expense.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();
        db.RecurringExpenses.Remove(expense);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Guid id, Guid userId, string name, decimal amount, string currency, int chargeDay, CancellationToken ct = default)
    {
        var expense = await db.RecurringExpenses.FindAsync([id], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(expense.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        expense.Name = name;
        expense.DefinitionAmount = amount;
        expense.DefinitionCurrency = currency.ToUpperInvariant();
        expense.ChargeDayOfMonth = Math.Clamp(chargeDay, 1, 28);

        var context = await db.BudgetContexts.FindAsync([expense.ContextId], ct)!;
        var period = await periodService.EnsureActivePeriodAsync(context!, ct);
        var baseAmount = currency.Equals("RSD", StringComparison.OrdinalIgnoreCase)
            ? amount
            : await exchangeRateService.ConvertToBaseAsync(amount, currency, period.StartDate, expense.ContextId, period.Id, ct);

        var items = await db.PeriodRecurringItems
            .Where(i => i.RecurringExpenseId == id && i.PeriodId == period.Id)
            .ToListAsync(ct);
        foreach (var item in items)
            item.PlannedBaseAmount = baseAmount;

        await db.SaveChangesAsync(ct);
    }

    public async Task AutoMarkDueItemsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today.Day > 28) return;

        var items = await db.PeriodRecurringItems
            .Include(i => i.RecurringExpense)
            .Include(i => i.Period)
            .Where(i => !i.IsPaid && !i.IsSkipped && !i.Period.IsClosed &&
                        i.RecurringExpense.ChargeDayOfMonth == today.Day &&
                        i.Period.StartDate <= today && i.Period.EndDate >= today)
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.IsPaid = true;
            item.PaidAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}
