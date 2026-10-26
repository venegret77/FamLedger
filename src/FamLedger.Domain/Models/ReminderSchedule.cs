namespace FamLedger.Domain.Models;

public class ReminderSchedule
{
    public bool SameTimeEveryDay { get; set; } = true;

    /// <summary>
    /// Enabled weekdays in UTC. Day is ISO: 1 = Monday … 7 = Sunday.
    /// </summary>
    public List<ReminderDaySlot> Days { get; set; } = [];
}

public class ReminderDaySlot
{
    public int Day { get; set; }
    public bool Enabled { get; set; }
    public TimeOnly TimeUtc { get; set; }
}
