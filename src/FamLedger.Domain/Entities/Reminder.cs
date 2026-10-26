using FamLedger.Domain.Enums;

namespace FamLedger.Domain.Entities;

public class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public ReminderKind Kind { get; set; } = ReminderKind.Custom;
    public string? Message { get; set; }
    public TimeOnly? TimeUtc { get; set; }

    /// <summary>
    /// JSON schedule. Null means every day at <see cref="TimeUtc"/>.
    /// </summary>
    public string? ScheduleJson { get; set; }

    public int[] ThresholdPercents { get; set; } = [];
    public ReminderAudience Audience { get; set; } = ReminderAudience.Self;
    public bool IsEnabled { get; set; } = true;
    public DateOnly? LastFiredDateUtc { get; set; }
    public DateTime? LastFiredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public BudgetContext Context { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ReminderThresholdFire> ThresholdFires { get; set; } = [];
}
