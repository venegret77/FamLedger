namespace FamLedger.Domain.Entities;

public class ReminderThresholdFire
{
    public Guid ReminderId { get; set; }
    public int ThresholdPercent { get; set; }
    public DateOnly LastFiredDateUtc { get; set; }

    public Reminder Reminder { get; set; } = null!;
}
