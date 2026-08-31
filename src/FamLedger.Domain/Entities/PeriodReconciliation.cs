namespace FamLedger.Domain.Entities;

public class PeriodReconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public Guid ContextId { get; set; }
    public string AssetItemsJson { get; set; } = "[]";
    public string ObligationItemsJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }

    public BudgetPeriod Period { get; set; } = null!;
    public BudgetContext Context { get; set; } = null!;
}
