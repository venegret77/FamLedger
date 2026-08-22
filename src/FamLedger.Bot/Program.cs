using FamLedger.Bot.Workers;
using FamLedger.Interfaces.Settings;
using FamLedger.Repository;
using FamLedger.Services.Extensions;
using FamLedger.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFamLedgerSettings(builder.Configuration);
builder.Services.AddFamLedgerDb(builder.Configuration);
builder.Services.AddFamLedgerRedis(builder.Configuration);
builder.Services.AddFamLedgerServices(includeTelegramBot: true);

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
    new TelegramBotClient(sp.GetRequiredService<IAppSettings>().TelegramBotToken));
builder.Services.AddSingleton<TelegramBot>();

builder.Services.AddHostedService<TelegramWorker>();
// Periods close only via "Начать новый месяц" — no automatic rollover.
builder.Services.AddHostedService<RecurringChargeWorker>();
builder.Services.AddHostedService<ExchangeRateWorker>();
builder.Services.AddHostedService<ReminderWorker>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["CONNECTION_STRING"] ?? "")
    .AddRedis(builder.Configuration["Redis:ConnectionString"] ?? builder.Configuration["REDIS_CONNECTION_STRING"] ?? "localhost:6382");

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

await host.RunAsync();
