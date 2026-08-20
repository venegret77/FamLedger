namespace FamLedger.Interfaces.Settings;

public interface IAppSettings
{
    string TelegramBotToken { get; }
    string JwtSecret { get; }
    string JwtIssuer { get; }
    int JwtExpiryHours { get; }
    string MinioEndpoint { get; }
    string MinioAccessKey { get; }
    string MinioSecretKey { get; }
    string MinioBucket { get; }
    string KursApiBaseUrl { get; }
    string WebPushPublicKey { get; }
    string WebPushPrivateKey { get; }
    string WebPushSubject { get; }
    int TelegramUpdateWorkerCount { get; }
    string WebPublicUrl { get; }
}
