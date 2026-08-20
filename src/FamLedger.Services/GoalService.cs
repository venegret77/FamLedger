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
    ISavingsService savingsService,
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
        await RefreshCompletionFromSavingsAsync(contextId, ct);
        return goal;
    }

    public async Task ContributeAsync(
        Guid goalId,
        Guid userId,
        decimal amount,
        string currency,
        CancellationToken ct = default)
    {
        // Взнос в цель = пополнение копилки; прогресс целей считается из баланса.
        var goal = await db.Goals.FindAsync([goalId], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(goal.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var period = await db.BudgetPeriods
            .Where(p => p.ContextId == goal.ContextId && !p.IsClosed)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No active period.");

        await savingsService.AddDepositAsync(
            goal.ContextId, period.Id, amount, currency, userId, ct);
        await RefreshCompletionFromSavingsAsync(goal.ContextId, ct);
    }

    public async Task<IReadOnlyList<Goal>> GetByContextAsync(Guid contextId, CancellationToken ct = default)
    {
        var goals = await db.Goals
            .Include(g => g.Contributions)
            .Where(g => g.ContextId == contextId)
            .ToListAsync(ct);
        return goals;
    }

    public async Task<decimal> GetProgressFromSavingsAsync(
        Guid contextId,
        string goalCurrency,
        decimal balanceInBase,
        CancellationToken ct = default)
    {
        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        return await ConvertToGoalCurrencyAsync(
            balanceInBase, context.BaseCurrency, goalCurrency, contextId, ct);
    }

    public async Task RefreshCompletionFromSavingsAsync(Guid contextId, CancellationToken ct = default)
    {
        var balance = await savingsService.GetTotalBalanceAsync(contextId, ct);
        var goals = await db.Goals
            .Where(g => g.ContextId == contextId && !g.IsCompleted)
            .ToListAsync(ct);

        foreach (var goal in goals)
        {
            var progress = await GetProgressFromSavingsAsync(contextId, goal.Currency, balance, ct);
            if (progress < goal.TargetAmount) continue;

            goal.IsCompleted = true;
            goal.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await notificationService.NotifyContextMembersAsync(contextId,
                $"🎯 Цель «{goal.Name}» достигнута! ({MoneyFormatter.Format(progress, goal.Currency)})", ct);

            var memberUserIds = await db.ContextMembers
                .Where(m => m.ContextId == contextId)
                .Select(m => m.UserId)
                .ToListAsync(ct);
            foreach (var memberId in memberUserIds)
            {
                await notificationService.DispatchWebhooksAsync(memberId, "goal.completed",
                    new { goalId = goal.Id, goal.Name, total = progress }, ct);
            }
        }
    }

    public async Task CheckAndNotifyCompletedAsync(Guid goalId, CancellationToken ct = default)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == goalId, ct);
        if (goal is null || goal.IsCompleted) return;
        await RefreshCompletionFromSavingsAsync(goal.ContextId, ct);
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
