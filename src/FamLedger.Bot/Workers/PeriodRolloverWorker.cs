using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Bot.Workers;

public class PeriodRolloverWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var periodService = scope.ServiceProvider.GetRequiredService<IBudgetPeriodService>();

                var contexts = await db.BudgetContexts.ToListAsync(stoppingToken);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                foreach (var context in contexts)
                {
                    var active = await db.BudgetPeriods
                        .FirstOrDefaultAsync(p => p.ContextId == context.Id && !p.IsClosed, stoppingToken);
                    if (active is not null && today > active.EndDate)
                        await periodService.ClosePeriodAsync(active, context, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PeriodRolloverWorker: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
