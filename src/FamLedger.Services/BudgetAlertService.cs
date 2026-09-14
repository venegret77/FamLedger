using FamLedger.Common;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class BudgetAlertService(
    AppDbContext db,
    IReminderService reminders,
    IBudgetPeriodService periodService,
    IBudgetCalculatorService calculator,
    INotificationService notifications) : IBudgetAlertService
{
    public async Task ReconcileFiresAfterSpendChangeAsync(Guid contextId, CancellationToken ct = default)
    {
        var context = await db.BudgetContexts.FindAsync([contextId], ct);
        if (context is null) return;

        var alerts = await reminders.GetEnabledBudgetAlertsAsync(contextId, ct);
        if (alerts.Count == 0) return;

        var period = await periodService.EnsureActivePeriodAsync(context, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await calculator.CalculateAsync(context, period, today, ct);
        var percentUsed = BudgetSummaryFormatter.TryGetDailySpendPercent(summary) ?? 0;
        if (summary.AvailableToday < 0)
            percentUsed = Math.Max(percentUsed, 100);

        foreach (var reminder in alerts)
        {
            await ClearUncrossedFiresAsync(reminder.Id, percentUsed, today, ct);
        }
    }

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
        var percent = BudgetSummaryFormatter.TryGetDailySpendPercent(summary);
        if (percent is null && summary.AvailableToday >= 0)
            return null;

        var percentUsed = percent ?? 100;
        if (summary.AvailableToday < 0 && percentUsed < 100)
            percentUsed = 100;

        BudgetAlertInfo? forClient = null;

        foreach (var reminder in alerts)
        {
            var appliesToActor = reminder.Audience == ReminderAudience.Family
                || reminder.CreatedByUserId == actingUserId;
            if (!appliesToActor) continue;

            var thresholds = ThresholdPercentHelper.Normalize(
                reminder.ThresholdPercents, ThresholdPercentHelper.DefaultBudgetAlert);

            await ClearUncrossedFiresAsync(reminder.Id, percentUsed, today, ct);

            var activeThresholds = thresholds.Where(t => percentUsed >= t).ToList();
            if (activeThresholds.Count == 0) continue;

            var highest = activeThresholds[^1];
            var message = BudgetSummaryFormatter.FormatBudgetAlert(
                summary, context.BaseCurrency, percentUsed, highest);
            var overBudget = summary.AvailableToday < 0 || percentUsed >= 100;
            forClient ??= new BudgetAlertInfo(message, percentUsed, highest, overBudget);

            if (!notifyViaTelegram) continue;

            var firedToday = await reminders.GetFiredThresholdsAsync(reminder.Id, today, ct);
            var newlyCrossed = ThresholdPercentHelper.GetNewlyCrossed(thresholds, percentUsed, firedToday);
            if (newlyCrossed.Count == 0) continue;

            foreach (var threshold in newlyCrossed)
            {
                var tgMessage = BudgetSummaryFormatter.FormatBudgetAlert(
                    summary, context.BaseCurrency, percentUsed, threshold);
                if (reminder.Audience == ReminderAudience.Family)
                    await notifications.NotifyContextMembersAsync(reminder.ContextId, tgMessage, ct);
                else if (reminder.CreatedByUser is not null)
                    await notifications.SendTelegramAsync(reminder.CreatedByUser.TelegramUserId, tgMessage, ct);
            }

            await reminders.MarkThresholdsFiredAsync(reminder.Id, newlyCrossed, today, ct);
        }

        return forClient;
    }

    private async Task ClearUncrossedFiresAsync(
        Guid reminderId,
        int percentUsed,
        DateOnly todayUtc,
        CancellationToken ct)
    {
        var stale = await db.ReminderThresholdFires
            .Where(f =>
                f.ReminderId == reminderId &&
                f.LastFiredDateUtc == todayUtc &&
                f.ThresholdPercent > percentUsed)
            .ToListAsync(ct);
        if (stale.Count == 0) return;

        db.ReminderThresholdFires.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
    }
}
