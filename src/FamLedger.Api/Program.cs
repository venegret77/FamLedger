using FamLedger.Api.Extensions;
using FamLedger.Interfaces.Settings;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFamLedgerSettings(builder.Configuration);
var settings = new AppSettings();
builder.Configuration.GetSection("App").Bind(settings);
builder.Configuration.Bind(settings);
settings.TelegramBotToken = builder.Configuration["TELEGRAM_BOT_TOKEN"] ?? settings.TelegramBotToken;
settings.JwtSecret = builder.Configuration["JWT_SECRET"] ?? settings.JwtSecret;

builder.Services.AddFamLedgerDb(builder.Configuration);
builder.Services.AddFamLedgerRedis(builder.Configuration);
builder.Services.AddFamLedgerServices(includeTelegramBot: false);
builder.Services.AddFamLedgerAuth(settings);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration["WEB_ORIGIN"] ?? "http://localhost:5173",
                "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["CONNECTION_STRING"] ?? "")
    .AddRedis(builder.Configuration["Redis:ConnectionString"] ?? builder.Configuration["REDIS_CONNECTION_STRING"] ?? "localhost:6382");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    try
    {
        var rates = scope.ServiceProvider.GetRequiredService<FamLedger.Interfaces.Services.IExchangeRateService>();
        await rates.FetchAndStoreRatesAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to fetch exchange rates on startup");
    }
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
