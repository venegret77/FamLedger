using FamLedger.Telegram;
using Telegram.Bot;

namespace FamLedger.Bot.Workers;

public class TelegramWorker(TelegramBot bot) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        bot.StartReceivingAsync(stoppingToken);
}
