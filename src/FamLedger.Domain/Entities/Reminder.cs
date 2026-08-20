using FamLedger.Domain.Enums;

namespace FamLedger.Domain.Entities;

public class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeOnly TimeUtc { get; set; }
    public ReminderAudience Audience { get; set; } = ReminderAudience.Self;
    public bool IsEnabled { get; set; } = true;
    public DateOnly? LastFiredDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public BudgetContext Context { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
