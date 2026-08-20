using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;

namespace FamLedger.Bot.Workers;

public class ReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ReminderWorker> logger) : BackgroundService
{
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

        var now = DateTime.UtcNow;
        var timeUtc = TimeOnly.FromDateTime(now);
        var todayUtc = DateOnly.FromDateTime(now);

        var due = await reminders.GetDueAsync(timeUtc, todayUtc, ct);
        foreach (var reminder in due)
        {
            try
            {
                if (reminder.Audience == ReminderAudience.Family)
                {
                    await notifications.NotifyContextMembersAsync(reminder.ContextId, reminder.Message, ct);
                }
                else
                {
                    await notifications.SendTelegramAsync(
                        reminder.CreatedByUser.TelegramUserId,
                        reminder.Message,
                        ct);
                }

                await reminders.MarkFiredAsync(reminder.Id, todayUtc, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fire reminder {ReminderId}", reminder.Id);
            }
        }
    }
}
