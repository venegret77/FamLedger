using FamLedger.Interfaces.Services;

namespace FamLedger.Bot.Workers;

public class RecurringChargeWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddHours(9);
            if (now > nextRun) nextRun = nextRun.AddDays(1);
            await Task.Delay(nextRun - now, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var recurringService = scope.ServiceProvider.GetRequiredService<IRecurringExpenseService>();
                await recurringService.AutoMarkDueItemsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RecurringChargeWorker: {ex.Message}");
            }
        }
    }
}
