namespace FamLedger.Domain.Entities;

public class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public string Currency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }

    public BudgetContext Context { get; set; } = null!;
    public ICollection<GoalContribution> Contributions { get; set; } = new List<GoalContribution>();
}
