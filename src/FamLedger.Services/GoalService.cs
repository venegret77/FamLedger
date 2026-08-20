using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class GoalService(
    AppDbContext db,
    IContextService contextService,
    IExchangeRateService exchangeRateService,
    INotificationService notificationService) : IGoalService
{
    public async Task<Goal> CreateAsync(
        Guid contextId,
        Guid userId,
        string name,
        decimal targetAmount,
        string currency,
        CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        var code = string.IsNullOrWhiteSpace(currency)
            ? context.BaseCurrency
            : currency.ToUpperInvariant();

        var goal = new Goal
        {
            ContextId = contextId,
            Name = name,
            TargetAmount = targetAmount,
            Currency = code
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task ContributeAsync(
        Guid goalId,
        Guid userId,
        decimal amount,
        string currency,
        CancellationToken ct = default)
    {
        var goal = await db.Goals.FindAsync([goalId], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(goal.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var amountInGoalCurrency = await ConvertToGoalCurrencyAsync(
            amount, currency, goal.Currency, goal.ContextId, ct);

        db.GoalContributions.Add(new GoalContribution
        {
            GoalId = goalId,
            UserId = userId,
            Amount = amountInGoalCurrency
        });
        await db.SaveChangesAsync(ct);
        await CheckAndNotifyCompletedAsync(goalId, ct);
    }

    public async Task<IReadOnlyList<Goal>> GetByContextAsync(Guid contextId, CancellationToken ct = default)
    {
        var goals = await db.Goals
            .Include(g => g.Contributions)
            .Where(g => g.ContextId == contextId)
            .ToListAsync(ct);
        return goals;
    }

    public async Task CheckAndNotifyCompletedAsync(Guid goalId, CancellationToken ct = default)
    {
        var goal = await db.Goals.Include(g => g.Contributions).FirstOrDefaultAsync(g => g.Id == goalId, ct);
        if (goal is null || goal.IsCompleted) return;

        var total = goal.Contributions.Sum(c => c.Amount);
        if (total < goal.TargetAmount) return;

        goal.IsCompleted = true;
        goal.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await notificationService.NotifyContextMembersAsync(goal.ContextId,
            $"🎯 Цель «{goal.Name}» достигнута! ({MoneyFormatter.Format(total, goal.Currency)})", ct);

        var memberUserIds = await db.ContextMembers
            .Where(m => m.ContextId == goal.ContextId)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        foreach (var memberId in memberUserIds)
        {
            await notificationService.DispatchWebhooksAsync(memberId, "goal.completed",
                new { goalId = goal.Id, goal.Name, total }, ct);
        }
    }

    public async Task DeleteAsync(Guid goalId, Guid userId, CancellationToken ct = default)
    {
        var goal = await db.Goals.FindAsync([goalId], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(goal.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();
        db.Goals.Remove(goal);
        await db.SaveChangesAsync(ct);
    }

    private async Task<decimal> ConvertToGoalCurrencyAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        Guid contextId,
        CancellationToken ct)
    {
        var from = string.IsNullOrWhiteSpace(fromCurrency)
            ? toCurrency
            : fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
            return amount;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inBase = await exchangeRateService.ConvertToBaseAsync(
            amount, from, today, contextId, null, ct);
        var toRate = await exchangeRateService.GetRateAsync(to, today, contextId, null, ct);
        if (toRate <= 0)
            throw new InvalidOperationException($"Invalid rate for {to}.");
        return inBase / toRate;
    }
}
