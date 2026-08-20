namespace FamLedger.Interfaces.Services;

public interface ILoginTokenService
{
    Task<string> CreateAsync(long telegramUserId, CancellationToken ct = default);
    Task<long?> ConsumeAsync(string token, CancellationToken ct = default);
}
