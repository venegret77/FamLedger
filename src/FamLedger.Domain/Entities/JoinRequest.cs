using FamLedger.Domain.Enums;

namespace FamLedger.Domain.Entities;

public class JoinRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid UserId { get; set; }
    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }

    public BudgetContext Context { get; set; } = null!;
    public User User { get; set; } = null!;
}
