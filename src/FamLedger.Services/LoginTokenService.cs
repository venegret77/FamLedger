using FamLedger.Interfaces.Services;

namespace FamLedger.Services;

public class LoginTokenService(IRedisService redis) : ILoginTokenService
{
    private const int CodeLength = 6;
    private static string Key(string token) => $"login:bot:{token}";

    public async Task<string> CreateAsync(long telegramUserId, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var token = Random.Shared.Next(0, 1_000_000).ToString($"D{CodeLength}");
            var key = Key(token);
            if (await redis.ExistsAsync(key))
                continue;

            await redis.SetAsync(key, telegramUserId.ToString(), TimeSpan.FromMinutes(10));
            return token;
        }

        throw new InvalidOperationException("Could not allocate a unique login code");
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
        cleaned = new string(cleaned.Where(char.IsAsciiDigit).ToArray());
        return cleaned.Length == CodeLength ? cleaned : null;
    }
}
