using FamLedger.Domain.Enums;

namespace FamLedger.Domain.Entities;

public class UserActivityState
{
    public Guid UserId { get; set; }
    public Guid ContextId { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public UserActivityKind LastActivityKind { get; set; }
    public DateTime? LastRecordedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public BudgetContext Context { get; set; } = null!;
}
