using FamLedger.Common;
using FamLedger.Domain.Models;

namespace FamLedger.Services.Tests;

public class ReminderScheduleRulesTests
{
    [Fact]
    public void IsDue_Should_MatchSelectedUtcWeekdayAndTime()
    {
        var schedule = new ReminderSchedule
        {
            SameTimeEveryDay = false,
            Days =
            [
                new ReminderDaySlot { Day = 1, Enabled = true, TimeUtc = new TimeOnly(9, 0) },
                new ReminderDaySlot { Day = 3, Enabled = true, TimeUtc = new TimeOnly(18, 30) },
            ],
        };
        var json = ReminderScheduleRules.Serialize(schedule);

        var monday = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
        var tuesday = new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);
        var wednesday = new DateTime(2026, 10, 28, 18, 30, 0, DateTimeKind.Utc);

        Assert.True(ReminderScheduleRules.IsDue(json, null, null, null, monday));
        Assert.False(ReminderScheduleRules.IsDue(json, null, null, null, tuesday));
        Assert.True(ReminderScheduleRules.IsDue(json, null, null, null, wednesday));
    }

    [Fact]
    public void IsDue_Should_AllowLaterSlotSameUtcDay()
    {
        var schedule = ReminderScheduleRules.EveryDay(new TimeOnly(9, 0));
        schedule.SameTimeEveryDay = false;
        schedule.Days.Add(new ReminderDaySlot
        {
            Day = 1,
            Enabled = true,
            TimeUtc = new TimeOnly(18, 30),
        });
        var json = ReminderScheduleRules.Serialize(ReminderScheduleRules.Normalize(schedule));

        var firedAt = new DateTime(2026, 10, 26, 9, 0, 10, DateTimeKind.Utc);
        var morning = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
        var evening = new DateTime(2026, 10, 26, 18, 30, 0, DateTimeKind.Utc);

        Assert.False(ReminderScheduleRules.IsDue(json, null, firedAt, null, morning));
        Assert.True(ReminderScheduleRules.IsDue(json, null, firedAt, null, evening));
    }

    [Fact]
    public void IsDue_Should_TreatMissingScheduleAsEveryDayAtFallbackTime()
    {
        var due = new DateTime(2026, 10, 27, 18, 0, 0, DateTimeKind.Utc);
        var other = new DateTime(2026, 10, 27, 18, 1, 0, DateTimeKind.Utc);

        Assert.True(ReminderScheduleRules.IsDue(null, new TimeOnly(18, 0), null, null, due));
        Assert.False(ReminderScheduleRules.IsDue(null, new TimeOnly(18, 0), null, null, other));
        Assert.False(ReminderScheduleRules.IsDue(
            null,
            new TimeOnly(18, 0),
            null,
            DateOnly.FromDateTime(due),
            due));
    }
}
