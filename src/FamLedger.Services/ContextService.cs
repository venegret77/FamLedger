using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class ContextService(AppDbContext db) : IContextService
{
    public async Task<BudgetContext> CreatePersonalContextAsync(User user, CancellationToken ct = default)
    {
        var existing = await db.BudgetContexts
            .AnyAsync(c => c.IsPersonal && c.Members.Any(m => m.UserId == user.Id), ct);
        if (existing) throw new InvalidOperationException("Personal context already exists");

        var context = new BudgetContext
        {
            Name = "Личный бюджет",
            IsPersonal = true,
            InviteCode = InviteCodeGenerator.Generate()
        };
        db.BudgetContexts.Add(context);
        db.ContextMembers.Add(new ContextMember { ContextId = context.Id, UserId = user.Id, Role = FamilyMemberRole.Head });
        SeedDefaultCategories(context.Id);
        await db.SaveChangesAsync(ct);
        return context;
    }

    public async Task<BudgetContext> CreateFamilyContextAsync(User user, string name, CancellationToken ct = default)
    {
        var context = new BudgetContext
        {
            Name = name,
            IsPersonal = false,
            InviteCode = InviteCodeGenerator.Generate()
        };
        db.BudgetContexts.Add(context);
        db.ContextMembers.Add(new ContextMember { ContextId = context.Id, UserId = user.Id, Role = FamilyMemberRole.Head });
        SeedDefaultCategories(context.Id);
        await db.SaveChangesAsync(ct);
        return context;
    }

    public Task<BudgetContext?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.BudgetContexts.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<ContextMember?> GetMembershipAsync(Guid contextId, Guid userId, CancellationToken ct = default) =>
        db.ContextMembers.FirstOrDefaultAsync(m => m.ContextId == contextId && m.UserId == userId, ct);

    public async Task<JoinRequest> RequestJoinAsync(Guid userId, string inviteCode, CancellationToken ct = default)
    {
        var context = await db.BudgetContexts.FirstOrDefaultAsync(c => c.InviteCode == inviteCode && !c.IsPersonal, ct)
            ?? throw new InvalidOperationException("Invalid invite code");

        if (await db.ContextMembers.AnyAsync(m => m.ContextId == context.Id && m.UserId == userId, ct))
            throw new InvalidOperationException("Already a member");

        if (await db.JoinRequests.AnyAsync(r => r.ContextId == context.Id && r.UserId == userId && r.Status == JoinRequestStatus.Pending, ct))
            throw new InvalidOperationException("Request already pending");

        var request = new JoinRequest { ContextId = context.Id, UserId = userId };
        db.JoinRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task ApproveJoinAsync(Guid requestId, Guid approverUserId, FamilyMemberRole role = FamilyMemberRole.Member, CancellationToken ct = default)
    {
        var request = await db.JoinRequests.Include(r => r.Context).FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new InvalidOperationException("Request not found");

        var approver = await GetMembershipAsync(request.ContextId, approverUserId, ct);
        if (approver is null || !RolePermissions.CanApproveJoinRequests(approver.Role))
            throw new UnauthorizedAccessException();

        if (role == FamilyMemberRole.Head && approver.Role != FamilyMemberRole.Head)
            throw new UnauthorizedAccessException();

        if (role is not (FamilyMemberRole.Member or FamilyMemberRole.Assistant or FamilyMemberRole.Head))
            role = FamilyMemberRole.Member;

        request.Status = JoinRequestStatus.Approved;
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedByUserId = approverUserId;
        db.ContextMembers.Add(new ContextMember
        {
            ContextId = request.ContextId,
            UserId = request.UserId,
            Role = role
        });

        var joiningUser = await db.Users.FindAsync([request.UserId], ct);
        if (joiningUser is not null)
            joiningUser.ActiveContextId = request.ContextId;

        await db.SaveChangesAsync(ct);
    }

    public async Task RejectJoinAsync(Guid requestId, Guid approverUserId, CancellationToken ct = default)
    {
        var request = await db.JoinRequests.FindAsync([requestId], ct)
            ?? throw new InvalidOperationException("Request not found");

        var approver = await GetMembershipAsync(request.ContextId, approverUserId, ct);
        if (approver is null || !RolePermissions.CanApproveJoinRequests(approver.Role))
            throw new UnauthorizedAccessException();

        request.Status = JoinRequestStatus.Rejected;
        request.ResolvedAt = DateTime.UtcNow;
        request.ResolvedByUserId = approverUserId;
        await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<JoinRequest>> GetPendingRequestsAsync(Guid contextId, CancellationToken ct = default) =>
        db.JoinRequests
            .Include(r => r.User)
            .Where(r => r.ContextId == contextId && r.Status == JoinRequestStatus.Pending)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<JoinRequest>)t.Result, ct);

    public Task<IReadOnlyList<ContextMember>> GetMembersAsync(Guid contextId, CancellationToken ct = default) =>
        db.ContextMembers.Include(m => m.User).Where(m => m.ContextId == contextId).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ContextMember>)t.Result, ct);

    public async Task UpdateMemberRoleAsync(Guid contextId, Guid memberId, FamilyMemberRole role, Guid headUserId, CancellationToken ct = default)
    {
        var head = await GetMembershipAsync(contextId, headUserId, ct);
        if (head?.Role != FamilyMemberRole.Head) throw new UnauthorizedAccessException();

        var member = await db.ContextMembers.FindAsync([memberId], ct) ?? throw new InvalidOperationException("Member not found");
        if (member.ContextId != contextId) throw new InvalidOperationException("Member not found");
        member.Role = role;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid contextId, Guid memberId, Guid actorUserId, CancellationToken ct = default)
    {
        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found");
        if (context.IsPersonal)
            throw new InvalidOperationException("Cannot leave a personal budget");

        var actor = await GetMembershipAsync(contextId, actorUserId, ct)
            ?? throw new UnauthorizedAccessException();
        var member = await db.ContextMembers.FindAsync([memberId], ct)
            ?? throw new InvalidOperationException("Member not found");
        if (member.ContextId != contextId)
            throw new InvalidOperationException("Member not found");

        var removingSelf = member.UserId == actorUserId;
        if (!removingSelf && actor.Role != FamilyMemberRole.Head)
            throw new UnauthorizedAccessException();

        if (member.Role == FamilyMemberRole.Head)
        {
            var otherHeads = await db.ContextMembers.CountAsync(
                m => m.ContextId == contextId && m.Role == FamilyMemberRole.Head && m.Id != member.Id, ct);
            if (otherHeads == 0)
                throw new InvalidOperationException("Cannot remove the only head. Assign another head first.");
        }

        db.ContextMembers.Remove(member);

        var removedUser = await db.Users.FindAsync([member.UserId], ct);
        if (removedUser?.ActiveContextId == contextId)
        {
            var personalId = await db.ContextMembers
                .Where(m => m.UserId == member.UserId && m.Context.IsPersonal)
                .Select(m => (Guid?)m.ContextId)
                .FirstOrDefaultAsync(ct);
            removedUser.ActiveContextId = personalId;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateSettingsAsync(Guid contextId, int periodStartDay, string baseCurrency, Guid userId, CancellationToken ct = default)
    {
        var membership = await GetMembershipAsync(contextId, userId, ct);
        if (membership?.Role != FamilyMemberRole.Head) throw new UnauthorizedAccessException();

        var context = await db.BudgetContexts.FindAsync([contextId], ct) ?? throw new InvalidOperationException();
        context.PeriodStartDay = Math.Clamp(periodStartDay, 1, 28);
        context.BaseCurrency = baseCurrency.ToUpperInvariant();
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> RegenerateInviteCodeAsync(Guid contextId, Guid userId, CancellationToken ct = default)
    {
        var membership = await GetMembershipAsync(contextId, userId, ct);
        if (membership?.Role != FamilyMemberRole.Head) throw new UnauthorizedAccessException();

        var context = await db.BudgetContexts.FindAsync([contextId], ct) ?? throw new InvalidOperationException();
        context.InviteCode = InviteCodeGenerator.Generate();
        await db.SaveChangesAsync(ct);
        return context.InviteCode;
    }

    public Task<IReadOnlyList<BudgetContext>> GetUserContextsAsync(Guid userId, CancellationToken ct = default) =>
        db.ContextMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.Context)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<BudgetContext>)t.Result, ct);

    private void SeedDefaultCategories(Guid contextId)
    {
        foreach (var (name, kind, order) in DefaultCategories.Items)
        {
            db.Categories.Add(new Category
            {
                ContextId = contextId,
                Name = name,
                Kind = kind,
                SortOrder = order,
                IsDefault = true
            });
        }
    }
}
