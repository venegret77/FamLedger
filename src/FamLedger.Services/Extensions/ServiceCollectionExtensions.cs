using FamLedger.Interfaces.Services;
using FamLedger.Interfaces.Settings;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FamLedger.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFamLedgerSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection("App"));
        services.Configure<AppSettings>(options =>
        {
            options.TelegramBotToken = configuration["TELEGRAM_BOT_TOKEN"] ?? configuration["App:TelegramBotToken"] ?? string.Empty;
            options.JwtSecret = configuration["JWT_SECRET"] ?? configuration["App:JwtSecret"] ?? options.JwtSecret;
            options.MinioAccessKey = configuration["MINIO_ROOT_USER"] ?? configuration["App:MinioAccessKey"] ?? options.MinioAccessKey;
            options.MinioSecretKey = configuration["MINIO_ROOT_PASSWORD"] ?? configuration["App:MinioSecretKey"] ?? options.MinioSecretKey;
            options.MinioBucket = configuration["MINIO_BUCKET"] ?? configuration["App:MinioBucket"] ?? options.MinioBucket;
            options.WebPublicUrl = configuration["WEB_PUBLIC_URL"]
                ?? configuration["WEB_ORIGIN"]
                ?? configuration["App:WebPublicUrl"]
                ?? options.WebPublicUrl;
        });
        services.AddSingleton<IAppSettings>(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);
        return services;
    }

    public static IServiceCollection AddFamLedgerDb(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? "Host=localhost;Port=5435;Database=famledger;Username=postgres;Password=postgres";
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(conn));
        return services;
    }

    public static IServiceCollection AddFamLedgerRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConn = configuration["Redis:ConnectionString"]
            ?? configuration["REDIS_CONNECTION_STRING"]
            ?? "localhost:6382";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
        services.AddScoped<IRedisService, RedisService>();
        return services;
    }

    public static IServiceCollection AddFamLedgerServices(this IServiceCollection services, bool includeTelegramBot = false)
    {
        services.AddHttpClient("KursApi", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IAppSettings>();
            client.BaseAddress = new Uri(settings.KursApiBaseUrl.TrimEnd('/') + "/");
        });
        services.AddHttpClient("webhooks");

        services
            .AddScoped<IUserService, UserService>()
            .AddScoped<IContextService, ContextService>()
            .AddScoped<IBudgetPeriodService, BudgetPeriodService>()
            .AddScoped<IBudgetCalculatorService, BudgetCalculatorService>()
            .AddScoped<IExchangeRateService, ExchangeRateService>()
            .AddScoped<IExpenseService, ExpenseService>()
            .AddScoped<ICategoryService, CategoryService>()
            .AddScoped<IRecurringExpenseService, RecurringExpenseService>()
            .AddScoped<IOneOffExpenseService, OneOffExpenseService>()
            .AddScoped<IIncomeService, IncomeService>()
            .AddScoped<IDebtService, DebtService>()
            .AddScoped<ISavingsService, SavingsService>()
            .AddScoped<IGoalService, GoalService>()
            .AddScoped<IReminderService, ReminderService>()
            .AddScoped<IAuthService, AuthService>()
            .AddScoped<ILoginTokenService, LoginTokenService>()
            .AddScoped<IFileStorageService, FileStorageService>()
            .AddScoped<IDialogStateService, DialogStateService>();

        if (includeTelegramBot)
        {
            services.AddScoped<INotificationService>(sp =>
                new NotificationService(
                    sp.GetRequiredService<AppDbContext>(),
                    sp.GetRequiredService<Telegram.Bot.ITelegramBotClient>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<IOptions<AppSettings>>()));
        }
        else
        {
            services.AddScoped<INotificationService>(sp =>
                new NotificationService(
                    sp.GetRequiredService<AppDbContext>(),
                    null,
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<IOptions<AppSettings>>()));
        }

        return services;
    }
}
