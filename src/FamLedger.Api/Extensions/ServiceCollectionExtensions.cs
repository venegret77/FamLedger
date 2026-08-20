// Re-export shared extensions from Services layer
namespace FamLedger.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFamLedgerSettings(this IServiceCollection services, IConfiguration configuration) =>
        FamLedger.Services.Extensions.ServiceCollectionExtensions.AddFamLedgerSettings(services, configuration);

    public static IServiceCollection AddFamLedgerDb(this IServiceCollection services, IConfiguration configuration) =>
        FamLedger.Services.Extensions.ServiceCollectionExtensions.AddFamLedgerDb(services, configuration);

    public static IServiceCollection AddFamLedgerRedis(this IServiceCollection services, IConfiguration configuration) =>
        FamLedger.Services.Extensions.ServiceCollectionExtensions.AddFamLedgerRedis(services, configuration);

    public static IServiceCollection AddFamLedgerServices(this IServiceCollection services, bool includeTelegramBot = false) =>
        FamLedger.Services.Extensions.ServiceCollectionExtensions.AddFamLedgerServices(services, includeTelegramBot);
}
