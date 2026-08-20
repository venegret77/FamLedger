namespace FamLedger.Common;

public static class CacheKeys
{
    public static string UserProfile(Guid userId) => $"user:profile:{userId}";
    public static string BudgetSummary(Guid periodId) => $"budget:summary:{periodId}";
    public static string FxRates(DateOnly date) => $"fx:rates:{date:yyyy-MM-dd}";
    public static string Categories(Guid contextId) => $"categories:{contextId}";
    public static string BotDialog(long chatId) => $"bot:dialog:{chatId}";
}
