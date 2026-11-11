using System.Globalization;
using System.Security.Cryptography;
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
            ct.ThrowIfCancellationRequested();
            var token = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            if (await redis.TrySetAsync(Key(token), telegramUserId.ToString(CultureInfo.InvariantCulture), TimeSpan.FromMinutes(10)))
                return token;
        }

        throw new InvalidOperationException("Could not allocate a unique login code");
    }

    public async Task<long?> ConsumeAsync(string token, CancellationToken ct = default)
    {
        var normalized = NormalizeToken(token);
        if (normalized is null) return null;

        ct.ThrowIfCancellationRequested();
        var value = await redis.GetAndDeleteAsync(Key(normalized));
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    private static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var cleaned = token.Trim().Trim('`', '"', '\'');
        return cleaned.Length == CodeLength && cleaned.All(char.IsAsciiDigit) ? cleaned : null;
    }
}
