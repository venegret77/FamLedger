using FamLedger.Domain.Entities;

namespace FamLedger.Common;

public static class PeriodCloseRules
{
    /// <summary>Show "start new month" when this many days (inclusive) remain, or period already ended.</summary>
    public const int WindowDays = 2;

    public static bool CanStartNewPeriod(BudgetPeriod period, DateOnly today)
    {
        if (today > period.EndDate)
            return true;

        if (today < period.StartDate)
            return false;

        var daysRemaining = BudgetPeriodMath.GetDaysInPeriod(today, period.EndDate);
        return daysRemaining <= WindowDays;
    }
}
