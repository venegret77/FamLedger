namespace FamLedger.Common;

public static class BudgetPeriodMath
{
    public static int GetDaysInPeriod(DateOnly start, DateOnly end) =>
        end.DayNumber - start.DayNumber + 1;

    // Inclusive: 15→20 = 6 days.
    public static int GetDaysPassed(DateOnly start, DateOnly today)
    {
        if (today < start) return 0;
        return today.DayNumber - start.DayNumber + 1;
    }
}
