namespace FamLedger.Interfaces.Settings;

public class AppSettings : IAppSettings
{
    public string TelegramBotToken { get; set; } = string.Empty;
    public string JwtSecret { get; set; } = "change-me-in-production-min-32-chars!!";
    public string JwtIssuer { get; set; } = "FamLedger";
    public int JwtExpiryHours { get; set; } = 168;
    public string MinioEndpoint { get; set; } = "localhost:9000";
    public string MinioAccessKey { get; set; } = "minioadmin";
    public string MinioSecretKey { get; set; } = "minioadmin";
    public string MinioBucket { get; set; } = "famledger";
    public string KursApiBaseUrl { get; set; } = "https://kurs.resenje.org";
    public string WebPushPublicKey { get; set; } = string.Empty;
    public string WebPushPrivateKey { get; set; } = string.Empty;
    public string WebPushSubject { get; set; } = "mailto:admin@famledger.local";
    public int TelegramUpdateWorkerCount { get; set; } = 2;
    public string WebPublicUrl { get; set; } = "http://localhost:5173";
}
