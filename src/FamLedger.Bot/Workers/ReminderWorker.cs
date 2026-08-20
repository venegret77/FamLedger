using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Bot.Workers;

public class ReminderWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddHours(10);
            if (now > nextRun) nextRun = nextRun.AddDays(1);
            await Task.Delay(nextRun - now, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var calculator = scope.ServiceProvider.GetRequiredService<IBudgetCalculatorService>();
                var periodService = scope.ServiceProvider.GetRequiredService<IBudgetPeriodService>();

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var contexts = await db.BudgetContexts.Where(c => !c.IsPersonal).ToListAsync(stoppingToken);

                foreach (var context in contexts)
                {
                    var period = await periodService.EnsureActivePeriodAsync(context, stoppingToken);
                    var summary = await calculator.CalculateAsync(context, period, today, stoppingToken);
                    if (summary.Remaining < 0)
                    {
                        await notifications.NotifyContextMembersAsync(context.Id,
                            $"⚠️ Бюджет «{context.Name}»: перерасход {summary.Remaining:N0} RSD", stoppingToken);
                    }

                    var unpaid = await db.PeriodRecurringItems
                        .Include(i => i.RecurringExpense)
                        .Where(i => i.PeriodId == period.Id && !i.IsPaid && !i.IsSkipped &&
                                    i.RecurringExpense.ChargeDayOfMonth <= today.Day)
                        .CountAsync(stoppingToken);
                    if (unpaid > 0)
                    {
                        await notifications.NotifyContextMembersAsync(context.Id,
                            $"📋 {unpaid} неоплаченных постоянных расходов", stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReminderWorker: {ex.Message}");
            }
        }
    }
}
