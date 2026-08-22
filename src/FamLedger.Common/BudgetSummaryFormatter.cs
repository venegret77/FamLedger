using System.Text;
using FamLedger.Domain.Models;

namespace FamLedger.Common;

public static class BudgetSummaryFormatter
{
    public static string FormatStats(BudgetSummary summary, string currency, string? contextName = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(contextName))
            sb.AppendLine($"📊 {contextName}");
        if (!string.IsNullOrWhiteSpace(summary.PeriodLabel))
            sb.AppendLine($"Период: {summary.PeriodLabel}");
        sb.AppendLine($"Остаток: {MoneyFormatter.Format(summary.Remaining, currency)}");
        sb.AppendLine($"Дневной бюджет: {MoneyFormatter.Format(summary.DailyBudgetAtStart, currency)}");
        sb.AppendLine($"Доступно сегодня: {MoneyFormatter.Format(summary.AvailableToday, currency)}");
        sb.AppendLine($"Потрачено сегодня: {MoneyFormatter.Format(summary.SpentToday, currency)}");
        sb.AppendLine($"Факт периода: {MoneyFormatter.Format(summary.Spent, currency)}");
        if (summary.TopUps > 0)
            sb.AppendLine($"Пополнения: {MoneyFormatter.Format(summary.TopUps, currency)}");
        sb.Append($"Дней осталось: {summary.DaysRemaining}");
        return sb.ToString();
    }

    public static string FormatBudgetAlert(BudgetSummary summary, string currency, int percentUsed, int threshold)
    {
        var daily = MoneyFormatter.Format(summary.DailyBudgetAtStart, currency);
        var available = MoneyFormatter.Format(summary.AvailableToday, currency);

        if (summary.AvailableToday < 0 || percentUsed >= 100)
        {
            return $"⚠️ Вы вышли за рамки дневного бюджета.\n" +
                   $"Дневной бюджет: {daily}\n" +
                   $"Доступно сегодня: {available}";
        }

        return $"⚠️ Использовано уже {percentUsed}% дневного бюджета (порог {threshold}%).\n" +
               $"Дневной бюджет: {daily}\n" +
               $"Доступно сегодня: {available}";
    }

    /// <summary>
    /// Доля дневного бюджета, уже «съеденная» относительно «Доступно сегодня».
    /// 0% — доступно ≥ дневного; 80% — осталось 20% дневного; &gt;100% — ушли в минус.
    /// </summary>
    public static int? TryGetDailySpendPercent(BudgetSummary summary)
    {
        var daily = summary.DailyBudgetAtStart;
        if (daily <= 0)
            return summary.AvailableToday < 0 ? 100 : null;

        var used = daily - summary.AvailableToday;
        if (used <= 0) return 0;
        return (int)Math.Round(used / daily * 100m, MidpointRounding.AwayFromZero);
    }

    public static bool IsDailyBudgetAlertTriggered(
        BudgetSummary summary,
        int thresholdPercent,
        out int percentUsed)
    {
        percentUsed = 0;
        if (summary.AvailableToday < 0)
        {
            percentUsed = TryGetDailySpendPercent(summary) ?? 100;
            return true;
        }

        var percent = TryGetDailySpendPercent(summary);
        if (percent is null) return false;
        percentUsed = percent.Value;
        return percentUsed >= thresholdPercent;
    }
}
