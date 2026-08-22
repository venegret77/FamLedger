using FamLedger.Common;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;

namespace FamLedger.Services;

public class BudgetAlertService(
    AppDbContext db,
    IReminderService reminders,
    IBudgetPeriodService periodService,
    IBudgetCalculatorService calculator,
    INotificationService notifications) : IBudgetAlertService
{
    public async Task<BudgetAlertInfo?> EvaluateAfterExpenseAsync(
        Guid contextId,
        Guid actingUserId,
        bool notifyViaTelegram,
        CancellationToken ct = default)
    {
        var context = await db.BudgetContexts.FindAsync([contextId], ct);
        if (context is null) return null;

        var alerts = await reminders.GetEnabledBudgetAlertsAsync(contextId, ct);
        if (alerts.Count == 0) return null;

        var period = await periodService.EnsureActivePeriodAsync(context, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await calculator.CalculateAsync(context, period, today, ct);

        BudgetAlertInfo? forClient = null;

        foreach (var reminder in alerts)
        {
            var appliesToActor = reminder.Audience == ReminderAudience.Family
                || reminder.CreatedByUserId == actingUserId;
            if (!appliesToActor) continue;

            var threshold = reminder.ThresholdPercent ?? 80;
            if (!BudgetSummaryFormatter.IsDailyBudgetAlertTriggered(summary, threshold, out var percent))
                continue;

            var message = BudgetSummaryFormatter.FormatBudgetAlert(
                summary, context.BaseCurrency, percent, threshold);
            var overBudget = summary.AvailableToday < 0 || percent >= 100;

            forClient ??= new BudgetAlertInfo(message, percent, threshold, overBudget);

            if (!notifyViaTelegram) continue;
            if (reminder.LastFiredDateUtc == today) continue;

            if (reminder.Audience == ReminderAudience.Family)
                await notifications.NotifyContextMembersAsync(reminder.ContextId, message, ct);
            else if (reminder.CreatedByUser is not null)
                await notifications.SendTelegramAsync(reminder.CreatedByUser.TelegramUserId, message, ct);

            await reminders.MarkFiredAsync(reminder.Id, today, ct);
        }

        return forClient;
    }
}
