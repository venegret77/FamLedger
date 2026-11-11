namespace FamLedger.Domain.Models;

public class ReminderSchedule
{
    public bool SameTimeEveryDay { get; set; } = true;
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// Enabled ISO weekdays in the schedule's time zone, or UTC for legacy schedules.
    /// </summary>
    public List<ReminderDaySlot> Days { get; set; } = [];
}

public class ReminderDaySlot
{
    public int Day { get; set; }
    public bool Enabled { get; set; }
    public TimeOnly TimeUtc { get; set; }
    public TimeOnly? TimeLocal { get; set; }
}
