using System.Globalization;

namespace FamLedger.Common;

public static class PeriodLabelHelper
{
    private static readonly string[] MonthNames =
    [
        "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
        "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
    ];

    public static string GetPeriodLabel(DateOnly start) =>
        $"{MonthNames[start.Month - 1]} ({start.Year})";
}
