using FamLedger.Domain.Enums;

namespace FamLedger.Domain.Entities;

public class CategorySpendingLimit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public decimal LimitAmount { get; set; }
    public int[] ThresholdPercents { get; set; } = [50, 80];
    public ReminderAudience Audience { get; set; } = ReminderAudience.Self;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public BudgetContext Context { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<CategoryLimitThresholdFire> ThresholdFires { get; set; } = [];
}
