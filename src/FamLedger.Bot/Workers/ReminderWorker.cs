using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using FamLedger.Telegram;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;

namespace FamLedger.Bot.Workers;

public class ReminderWorker(
    IServiceScopeFactory scopeFactory,
    ITelegramBotClient botClient,
    ILogger<ReminderWorker> logger) : BackgroundService
{
    private const int ItemsPerMessage = 8;
    private static readonly TimeSpan QuietWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan DeliveryLease = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan DeliveryRetention = TimeSpan.FromDays(1);

    private sealed record Recipient(Guid UserId, long TelegramUserId, FamilyMemberRole Role);
    private sealed record ReminderMessage(string Text, InlineKeyboardMarkup? Keyboard = null);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FireDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "ReminderWorker failed");
            }

            var now = DateTime.UtcNow;
            var nextMinute = now.AddTicks(TimeSpan.TicksPerMinute - now.Ticks % TimeSpan.TicksPerMinute);
            await Task.Delay(nextMinute - now, stoppingToken);
        }
    }

    private async Task FireDueRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
        var now = DateTime.UtcNow;
        var due = await reminders.GetDueTimedAsync(now, ct);
        foreach (var reminder in due)
        {
            try
            {
                await DeliverOccurrenceAsync(scope.ServiceProvider, reminder, DateOnly.FromDateTime(now), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed to fire reminder {ReminderId}", reminder.Id);
            }
        }
    }

    private async Task DeliverOccurrenceAsync(
        IServiceProvider services, Reminder reminder, DateOnly todayUtc, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var reminders = services.GetRequiredService<IReminderService>();
        var activity = services.GetRequiredService<IUserActivityService>();
        var recipients = await ResolveRecipientsAsync(db, reminder, ct);
        recipients = await FilterByActivityAsync(activity, reminder, recipients, ct);
        if (recipients.Count == 0)
        {
            await reminders.CompleteTimedAsync(reminder, fired: false, ct);
            return;
        }

        var messages = await BuildMessagesAsync(services, reminder, recipients[0].UserId, todayUtc, ct);
        var allFinished = true;
        if (messages.Count > 0)
        {
            var redis = services.GetRequiredService<IRedisService>();
            foreach (var recipient in recipients)
            {
                var stillEligible = await db.ContextMembers.AnyAsync(member => member.ContextId == reminder.ContextId
                    && member.UserId == recipient.UserId, ct);
                if (stillEligible)
                    allFinished &= await TryDeliverAsync(redis, reminder, recipient, messages, ct);
            }
        }
        if (allFinished)
            await reminders.CompleteTimedAsync(reminder, fired: messages.Count > 0, ct);
    }

    private async Task<bool> TryDeliverAsync(
        IRedisService redis, Reminder reminder, Recipient recipient, IReadOnlyList<ReminderMessage> messages, CancellationToken ct)
    {
        var key = $"reminder:delivery:{reminder.Id:N}:{reminder.NextDueAtUtc!.Value.Ticks}:{recipient.UserId:N}";
        if (!await redis.TrySetAsync(key, "sending", DeliveryLease))
            return await redis.GetAsync(key) == "finished";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        try
        {
            foreach (var message in messages)
            {
                await botClient.SendMessage(recipient.TelegramUserId, message.Text,
                    replyMarkup: RolePermissions.CanManagePlan(recipient.Role) ? message.Keyboard : null,
                    disableNotification: true, cancellationToken: timeout.Token);
            }
        }
        catch (ApiRequestException ex) when (ex.ErrorCode is 400 or 403)
        {
            logger.LogDebug(ex, "Telegram rejected reminder {ReminderId}", reminder.Id);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await redis.DeleteAsync(key);
            logger.LogWarning(ex, "Could not deliver reminder {ReminderId}; will retry", reminder.Id);
            return false;
        }

        await redis.SetAsync(key, "finished", DeliveryRetention);
        return true;
    }

    private static async Task<IReadOnlyList<ReminderMessage>> BuildMessagesAsync(
        IServiceProvider services, Reminder reminder, Guid recipientId, DateOnly todayUtc, CancellationToken ct)
    {
        if (reminder.Kind == ReminderKind.Custom)
            return string.IsNullOrWhiteSpace(reminder.Message) ? [] : [new(reminder.Message)];
        if (reminder.Kind == ReminderKind.EveningCheckIn)
            return [new(reminder.Message ?? "Не забудь записать расходы за день ✍️")];
        if (reminder.Kind == ReminderKind.UnpaidDebts)
            return await BuildUnpaidDebtsAsync(services.GetRequiredService<IDebtService>(), reminder, todayUtc, ct);

        var context = reminder.Context;
        var periodService = services.GetRequiredService<IBudgetPeriodService>();
        var period = await periodService.EnsureActivePeriodAsync(context, ct);
        if (reminder.Kind == ReminderKind.UnpaidPlanned)
            return await BuildUnpaidPlannedAsync(services.GetRequiredService<AppDbContext>(), context, period, todayUtc, ct);

        var calculator = services.GetRequiredService<IBudgetCalculatorService>();
        var summary = await calculator.CalculateAsync(context, period, todayUtc, ct);
        if (reminder.Kind == ReminderKind.PeriodEnding)
        {
            if (summary.DaysRemaining > 3) return [];
            var balance = MoneyFormatter.Format(summary.Remaining, context.BaseCurrency);
            return [new($"⏳ До конца периода «{summary.PeriodLabel}» осталось {summary.DaysRemaining} дн.\nОстаток: {balance}")];
        }

        var limits = await services.GetRequiredService<ICategorySpendingLimitService>()
            .GetProgressAsync(context.Id, recipientId, ct);
        return [new(BudgetSummaryFormatter.FormatStats(summary, context.BaseCurrency, context.Name, limits))];
    }

    private static async Task<IReadOnlyList<ReminderMessage>> BuildUnpaidDebtsAsync(
        IDebtService debtService, Reminder reminder, DateOnly todayUtc, CancellationToken ct)
    {
        if (reminder.LastFiredDateUtc is { } last && todayUtc.DayNumber - last.DayNumber < 7)
            return [];

        var debts = await debtService.GetByContextAsync(reminder.ContextId, hidePaid: true, ct);
        var lines = new List<string>();
        foreach (var debt in debts)
        {
            var direction = debt.Direction == DebtDirection.TheyOwe ? "нам должны" : "мы должны";
            var balances = debt.Entries.Where(entry => !entry.IsPaid)
                .GroupBy(entry => entry.Currency, StringComparer.OrdinalIgnoreCase);
            foreach (var balance in balances)
            {
                var total = MoneyFormatter.Format(balance.Sum(entry => entry.RemainingAmount), balance.Key);
                lines.Add($"• {debt.CounterpartyName}: {total} ({direction})");
            }
        }
        return lines.Chunk(ItemsPerMessage).Select(batch =>
            new ReminderMessage("💳 Незакрытые долги:\n" + string.Join("\n", batch))).ToList();
    }

    private static async Task<IReadOnlyList<ReminderMessage>> BuildUnpaidPlannedAsync(
        AppDbContext db, BudgetContext context, BudgetPeriod period, DateOnly todayUtc, CancellationToken ct)
    {
        var items = await db.PeriodRecurringItems
            .AsNoTracking()
            .Include(item => item.RecurringExpense)
            .Where(item => item.PeriodId == period.Id && !item.IsPaid && !item.IsSkipped)
            .ToListAsync(ct);

        var due = items
            .Select(item => (Item: item, ChargeDate: ResolveChargeDateInPeriod(period, item.RecurringExpense.ChargeDayOfMonth)))
            .Where(entry => entry.ChargeDate is not null && entry.ChargeDate <= todayUtc)
            .OrderBy(entry => entry.ChargeDate)
            .ThenBy(entry => entry.Item.Id);
        var messages = new List<ReminderMessage>();
        foreach (var batch in due.Chunk(ItemsPerMessage))
        {
            var lines = batch.Select(entry =>
            {
                var name = entry.Item.RecurringExpense.Name;
                var amount = MoneyFormatter.Format(entry.Item.PlannedBaseAmount, context.BaseCurrency);
                var when = entry.ChargeDate == todayUtc ? "сегодня" : $"с {entry.ChargeDate:dd.MM}";
                return $"• {name}: {amount} ({when})";
            });
            var text = "📌 Неоплаченные плановые:\n" + string.Join("\n", lines);
            var buttons = batch.Select(entry => new[]
            {
                TelegramBot.CreatePlannedPaymentButton(entry.Item.Id, entry.Item.RecurringExpense.Name)
            });
            messages.Add(new ReminderMessage(text, new InlineKeyboardMarkup(buttons)));
        }
        return messages;
    }

    private static DateOnly? ResolveChargeDateInPeriod(BudgetPeriod period, int chargeDay)
    {
        chargeDay = Math.Clamp(chargeDay, 1, 28);
        for (var date = period.StartDate; date <= period.EndDate; date = date.AddDays(1))
        {
            if (date.Day == chargeDay) return date;
        }
        return null;
    }

    private static Task<List<Recipient>> ResolveRecipientsAsync(AppDbContext db, Reminder reminder, CancellationToken ct) =>
        db.ContextMembers
            .Where(member => member.ContextId == reminder.ContextId
                && (reminder.Audience == ReminderAudience.Family || member.UserId == reminder.CreatedByUserId))
            .Select(member => new Recipient(member.UserId, member.User.TelegramUserId, member.Role))
            .ToListAsync(ct);

    private static async Task<List<Recipient>> FilterByActivityAsync(
        IUserActivityService activity, Reminder reminder, List<Recipient> recipients, CancellationToken ct)
    {
        if (reminder.Kind is not (ReminderKind.DailyBalance or ReminderKind.EveningCheckIn))
            return recipients;

        var filtered = new List<Recipient>();
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
