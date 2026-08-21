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
        var envelope = summary.Income + summary.TopUps - summary.PlannedExpenses;
        if (percentUsed >= 100)
        {
            return $"⚠️ Вы вышли за рамки бюджета.\n" +
                   $"Потрачено: {MoneyFormatter.Format(summary.Spent, currency)}" +
                   (envelope > 0 ? $" из {MoneyFormatter.Format(envelope, currency)}" : "") +
                   $"\nОстаток: {MoneyFormatter.Format(summary.Remaining, currency)}";
        }

        return $"⚠️ Потрачено уже {percentUsed}% бюджета (порог {threshold}%).\n" +
               $"Факт: {MoneyFormatter.Format(summary.Spent, currency)}" +
               (envelope > 0 ? $" / {MoneyFormatter.Format(envelope, currency)}" : "") +
               $"\nОстаток: {MoneyFormatter.Format(summary.Remaining, currency)}";
    }

    public static int? TryGetSpendPercent(BudgetSummary summary)
    {
        var envelope = summary.Income + summary.TopUps - summary.PlannedExpenses;
        if (envelope <= 0) return summary.Spent > 0 ? 100 : null;
        return (int)Math.Round(summary.Spent / envelope * 100m, MidpointRounding.AwayFromZero);
    }
}
