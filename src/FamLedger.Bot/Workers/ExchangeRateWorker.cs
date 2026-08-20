using FamLedger.Interfaces.Services;

namespace FamLedger.Bot.Workers;

public class ExchangeRateWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var exchangeService = scope.ServiceProvider.GetRequiredService<IExchangeRateService>();
                await exchangeService.FetchAndStoreRatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExchangeRateWorker: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
