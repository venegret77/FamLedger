using FamLedger.Common;
using FamLedger.Domain.Models;
using Xunit;

namespace FamLedger.Services.Tests;

public sealed class ReminderOccurrenceTests
{
    [Fact]
    public void LocalHourRemainsTheSameOnBothSidesOfDaylightSaving()
    {
        var schedule = SundayAt(9, 0);
        Assert.Equal(Utc(2026, 3, 22, 8), ReminderScheduleRules.NextOccurrenceUtc(schedule, null, Utc(2026, 3, 22, 0)));
        Assert.Equal(Utc(2026, 3, 29, 7), ReminderScheduleRules.NextOccurrenceUtc(schedule, null, Utc(2026, 3, 29, 0)));
    }

    [Fact]
    public void SpringGapMovesToTheNextValidLocalMinute()
    {
        var schedule = SundayAt(2, 30);
        Assert.Equal(Utc(2026, 3, 29, 1), ReminderScheduleRules.NextOccurrenceUtc(schedule, null, Utc(2026, 3, 29, 0)));
    }

    [Fact]
    public void FallOverlapUsesOneOccurrence()
    {
        var schedule = SundayAt(2, 30);
        var occurrence = new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc);
        Assert.Equal(occurrence, ReminderScheduleRules.NextOccurrenceUtc(schedule, null, Utc(2026, 10, 25, 0)));
        Assert.Equal(occurrence.AddDays(7), ReminderScheduleRules.NextOccurrenceUtc(schedule, null, occurrence));
    }

    private static string SundayAt(int hour, int minute) =>
        ReminderScheduleRules.Serialize(new ReminderSchedule
        {
            TimeZoneId = "Europe/Belgrade",
            Days = [new ReminderDaySlot { Day = 7, Enabled = true, TimeLocal = new TimeOnly(hour, minute) }]
        });

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);
}
