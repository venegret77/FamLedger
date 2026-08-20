using FamLedger.Interfaces.Services;

namespace FamLedger.Services;

public class LoginTokenService(IRedisService redis) : ILoginTokenService
{
    private static string Key(string token) => $"login:bot:{token}";

    public async Task<string> CreateAsync(long telegramUserId, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        await redis.SetAsync(Key(token), telegramUserId.ToString(), TimeSpan.FromMinutes(10));
        return token;
    }

    public async Task<long?> ConsumeAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var key = Key(token.Trim());
        var value = await redis.GetAsync(key);
        if (value is null) return null;
        await redis.DeleteAsync(key);
        return long.TryParse(value, out var id) ? id : null;
    }
}
