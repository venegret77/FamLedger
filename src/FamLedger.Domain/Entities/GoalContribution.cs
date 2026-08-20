namespace FamLedger.Domain.Entities;

public class GoalContribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoalId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Goal Goal { get; set; } = null!;
    public User User { get; set; } = null!;
}
