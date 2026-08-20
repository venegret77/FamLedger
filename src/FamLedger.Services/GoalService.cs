using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class GoalService(
    AppDbContext db,
    IContextService contextService,
    INotificationService notificationService) : IGoalService
{
    public async Task<Goal> CreateAsync(Guid contextId, Guid userId, string name, decimal targetAmount, CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var goal = new Goal { ContextId = contextId, Name = name, TargetAmount = targetAmount };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task ContributeAsync(Guid goalId, Guid userId, decimal amount, CancellationToken ct = default)
    {
        var goal = await db.Goals.FindAsync([goalId], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(goal.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        db.GoalContributions.Add(new GoalContribution { GoalId = goalId, UserId = userId, Amount = amount });
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
}
