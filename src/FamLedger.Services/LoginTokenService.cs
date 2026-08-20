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
        var normalized = NormalizeToken(token);
        if (normalized is null) return null;

        var key = Key(normalized);
        var value = await redis.GetAsync(key);
        if (value is null) return null;
        await redis.DeleteAsync(key);
        return long.TryParse(value, out var id) ? id : null;
    }

    private static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var cleaned = token.Trim().Trim('`', '"', '\'');
        // Keep hex only — strips zero-width / markdown leftovers from Telegram copy.
        cleaned = new string(cleaned.Where(char.IsAsciiHexDigit).ToArray());
        return cleaned.Length == 32 ? cleaned.ToLowerInvariant() : null;
    }
}
