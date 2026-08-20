using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;

namespace FamLedger.Interfaces.Services;

public interface IContextService
{
    Task<BudgetContext> CreatePersonalContextAsync(User user, CancellationToken ct = default);
    Task<BudgetContext> CreateFamilyContextAsync(User user, string name, CancellationToken ct = default);
    Task<BudgetContext?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ContextMember?> GetMembershipAsync(Guid contextId, Guid userId, CancellationToken ct = default);
    Task<JoinRequest> RequestJoinAsync(Guid userId, string inviteCode, CancellationToken ct = default);
    Task ApproveJoinAsync(Guid requestId, Guid approverUserId, FamilyMemberRole role = FamilyMemberRole.Member, CancellationToken ct = default);
    Task RejectJoinAsync(Guid requestId, Guid approverUserId, CancellationToken ct = default);
    Task<IReadOnlyList<JoinRequest>> GetPendingRequestsAsync(Guid contextId, CancellationToken ct = default);
    Task<IReadOnlyList<ContextMember>> GetMembersAsync(Guid contextId, CancellationToken ct = default);
    Task UpdateMemberRoleAsync(Guid contextId, Guid memberId, FamilyMemberRole role, Guid headUserId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid contextId, Guid memberId, Guid actorUserId, CancellationToken ct = default);
    Task UpdateSettingsAsync(Guid contextId, int periodStartDay, string baseCurrency, Guid userId, CancellationToken ct = default);
    Task<string> RegenerateInviteCodeAsync(Guid contextId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetContext>> GetUserContextsAsync(Guid userId, CancellationToken ct = default);
}
