using FamLedger.Common;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Bot.Workers;

public class ReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan QuietWindow = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FireDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ReminderWorker failed");
            }

            var now = DateTime.UtcNow;
            var nextMinute = new DateTime(
                    now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc)
                .AddMinutes(1);
            var delay = nextMinute - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task FireDueRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var calculator = scope.ServiceProvider.GetRequiredService<IBudgetCalculatorService>();
        var periodService = scope.ServiceProvider.GetRequiredService<IBudgetPeriodService>();
        var debtService = scope.ServiceProvider.GetRequiredService<IDebtService>();
        var activity = scope.ServiceProvider.GetRequiredService<IUserActivityService>();
        var categoryLimits = scope.ServiceProvider.GetRequiredService<ICategorySpendingLimitService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var timeUtc = TimeOnly.FromDateTime(now);
        var todayUtc = DateOnly.FromDateTime(now);

        var dueTimed = await reminders.GetDueTimedAsync(timeUtc, todayUtc, ct);
        foreach (var reminder in dueTimed)
        {
            try
            {
                var message = await BuildTimedMessageAsync(
                    reminder, calculator, periodService, debtService, categoryLimits, db, todayUtc, ct);
                if (message is null) continue;

                var recipients = await ResolveRecipientsAsync(db, reminder, ct);
                recipients = await FilterByActivityAsync(activity, reminder, recipients, ct);

                if (recipients.Count > 0)
                {
                    await notifications.NotifyTelegramUsersAsync(
                        recipients.Select(r => r.TelegramUserId), message, ct);
                }

                await reminders.MarkFiredAsync(reminder.Id, todayUtc, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fire reminder {ReminderId}", reminder.Id);
            }
        }
    }

    private static async Task<string?> BuildTimedMessageAsync(
        Domain.Entities.Reminder reminder,
        IBudgetCalculatorService calculator,
        IBudgetPeriodService periodService,
        IDebtService debtService,
        ICategorySpendingLimitService categoryLimitService,
        AppDbContext db,
        DateOnly todayUtc,
        CancellationToken ct)
    {
        switch (reminder.Kind)
        {
            case ReminderKind.Custom:
                return string.IsNullOrWhiteSpace(reminder.Message) ? null : reminder.Message;

            case ReminderKind.EveningCheckIn:
                return string.IsNullOrWhiteSpace(reminder.Message)
                    ? "Не забудь записать расходы за день ✍️"
                    : reminder.Message;

            case ReminderKind.DailyBalance:
            {
                var context = reminder.Context;
                if (context is null) return null;
                var period = await periodService.EnsureActivePeriodAsync(context, ct);
                var summary = await calculator.CalculateAsync(context, period, todayUtc, ct);
                var limits = await categoryLimitService.GetProgressAsync(
                    reminder.ContextId, reminder.CreatedByUserId, ct);
                return BudgetSummaryFormatter.FormatStats(
                    summary, context.BaseCurrency, context.Name, limits);
            }

            case ReminderKind.PeriodEnding:
            {
                var context = reminder.Context;
                if (context is null) return null;
                var period = await periodService.EnsureActivePeriodAsync(context, ct);
                var summary = await calculator.CalculateAsync(context, period, todayUtc, ct);
                if (summary.DaysRemaining > 3) return null;
                return $"⏳ До конца периода «{summary.PeriodLabel}» осталось {summary.DaysRemaining} дн.\n" +
                       $"Остаток: {MoneyFormatter.Format(summary.Remaining, context.BaseCurrency)}";
            }

            case ReminderKind.UnpaidDebts:
            {
                if (reminder.LastFiredDateUtc is { } last
                    && todayUtc.DayNumber - last.DayNumber < 7)
                    return null;

                var debts = await debtService.GetByContextAsync(reminder.ContextId, hidePaid: true, ct);
                if (debts.Count == 0) return null;

                var lines = debts.Select(d =>
                {
                    var open = d.Entries.Where(e => !e.IsPaid).ToList();
                    var bal = open.Sum(e => e.RemainingAmount);
                    var cur = open.FirstOrDefault()?.Currency ?? "RSD";
                    var dir = d.Direction == DebtDirection.TheyOwe ? "нам должны" : "мы должны";
                    return $"• {d.CounterpartyName}: {MoneyFormatter.Format(bal, cur)} ({dir})";
                });
                return "💳 Незакрытые долги:\n" + string.Join("\n", lines);
            }

            case ReminderKind.UnpaidPlanned:
            {
                var context = reminder.Context;
                if (context is null) return null;
                var period = await periodService.EnsureActivePeriodAsync(context, ct);
                var items = await db.PeriodRecurringItems
                    .Include(i => i.RecurringExpense)
                    .Where(i => i.PeriodId == period.Id && !i.IsPaid && !i.IsSkipped)
                    .ToListAsync(ct);

                var due = items
                    .Select(item => (Item: item, ChargeDate: ResolveChargeDateInPeriod(period, item.RecurringExpense.ChargeDayOfMonth)))
                    .Where(x => x.ChargeDate is not null && x.ChargeDate <= todayUtc)
                    .OrderBy(x => x.ChargeDate)
                    .ToList();

                if (due.Count == 0) return null;

                var lines = due.Select(x =>
                {
                    var name = x.Item.RecurringExpense.Name;
                    var amount = MoneyFormatter.Format(x.Item.PlannedBaseAmount, context.BaseCurrency);
                    var when = x.ChargeDate == todayUtc
                        ? "сегодня"
                        : $"с {x.ChargeDate:dd.MM}";
                    return $"• {name}: {amount} ({when})";
                });
                return "📌 Неоплаченные плановые:\n" + string.Join("\n", lines);
            }

            default:
                return null;
        }
    }

    private static DateOnly? ResolveChargeDateInPeriod(Domain.Entities.BudgetPeriod period, int chargeDay)
    {
        chargeDay = Math.Clamp(chargeDay, 1, 28);
        for (var d = period.StartDate; d <= period.EndDate; d = d.AddDays(1))
        {
            if (d.Day == chargeDay)
                return d;
        }
        return null;
    }

    private static async Task<List<(Guid UserId, long TelegramUserId)>> ResolveRecipientsAsync(
        AppDbContext db,
        Domain.Entities.Reminder reminder,
        CancellationToken ct)
    {
        if (reminder.Audience == ReminderAudience.Family)
        {
            return await db.ContextMembers
                .AsNoTracking()
                .Where(m => m.ContextId == reminder.ContextId)
                .Select(m => new ValueTuple<Guid, long>(m.UserId, m.User.TelegramUserId))
                .ToListAsync(ct);
        }

        if (reminder.CreatedByUser is null) return [];
        return [(reminder.CreatedByUserId, reminder.CreatedByUser.TelegramUserId)];
    }

    private static async Task<List<(Guid UserId, long TelegramUserId)>> FilterByActivityAsync(
        IUserActivityService activity,
        Domain.Entities.Reminder reminder,
        List<(Guid UserId, long TelegramUserId)> recipients,
        CancellationToken ct)
    {
        if (reminder.Kind is not (ReminderKind.DailyBalance or ReminderKind.EveningCheckIn))
            return recipients;

        var filtered = new List<(Guid UserId, long TelegramUserId)>();
        foreach (var recipient in recipients)
        {
            var skip = reminder.Kind == ReminderKind.DailyBalance
                ? await activity.WasActiveWithinAsync(recipient.UserId, reminder.ContextId, QuietWindow, ct)
                : await activity.WasRecordedWithinAsync(recipient.UserId, reminder.ContextId, QuietWindow, ct);
            if (!skip)
                filtered.Add(recipient);
        }

        return filtered;
    }
}
