using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Bot.Workers;

public class ReminderWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly int[] ReminderHoursUtc = [10, 21];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = NextRunUtc(now);
            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            try
            {
                if (nextRun.Hour == 10)
                    await RunMorningRemindersAsync(stoppingToken);
                else if (nextRun.Hour == 21)
                    await RunEveningExpenseRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReminderWorker: {ex.Message}");
            }
        }
    }

    private static DateTime NextRunUtc(DateTime now)
    {
        foreach (var hour in ReminderHoursUtc)
        {
            var candidate = now.Date.AddHours(hour);
            if (candidate > now)
                return candidate;
        }

        return now.Date.AddDays(1).AddHours(ReminderHoursUtc[0]);
    }

    private async Task RunMorningRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var calculator = scope.ServiceProvider.GetRequiredService<IBudgetCalculatorService>();
        var periodService = scope.ServiceProvider.GetRequiredService<IBudgetPeriodService>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var contexts = await db.BudgetContexts.Where(c => !c.IsPersonal).ToListAsync(ct);

        foreach (var context in contexts)
        {
            var period = await periodService.EnsureActivePeriodAsync(context, ct);
            var summary = await calculator.CalculateAsync(context, period, today, ct);
            if (summary.Remaining < 0)
            {
                await notifications.NotifyContextMembersAsync(context.Id,
                    $"⚠️ Бюджет «{context.Name}»: перерасход {summary.Remaining:N0} RSD", ct);
            }

            var unpaid = await db.PeriodRecurringItems
                .Include(i => i.RecurringExpense)
                .Where(i => i.PeriodId == period.Id && !i.IsPaid && !i.IsSkipped &&
                            i.RecurringExpense.ChargeDayOfMonth <= today.Day)
                .CountAsync(ct);
            if (unpaid > 0)
            {
                await notifications.NotifyContextMembersAsync(context.Id,
                    $"📋 {unpaid} неоплаченных постоянных расходов", ct);
            }
        }
    }

    private async Task RunEveningExpenseRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var contexts = await db.BudgetContexts.Where(c => !c.IsPersonal).ToListAsync(ct);

        foreach (var context in contexts)
        {
            await notifications.NotifyContextMembersAsync(context.Id,
                $"📝 Не забудь записать сегодняшние расходы в «{context.Name}»", ct);
        }
    }
}
