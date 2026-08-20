namespace FamLedger.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long TelegramUserId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarKey { get; set; }
    public Guid? ActiveContextId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BudgetContext? ActiveContext { get; set; }
    public ICollection<ContextMember> Memberships { get; set; } = new List<ContextMember>();
    public ICollection<JoinRequest> JoinRequests { get; set; } = new List<JoinRequest>();
}
