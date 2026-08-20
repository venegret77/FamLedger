using FamLedger.Domain.Enums;

namespace FamLedger.Domain.Entities;

public class ContextMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid UserId { get; set; }
    public FamilyMemberRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public BudgetContext Context { get; set; } = null!;
    public User User { get; set; } = null!;
}
