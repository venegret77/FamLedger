using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FamLedger.Interfaces.Services;
using FamLedger.Interfaces.Settings;
using Microsoft.IdentityModel.Tokens;

namespace FamLedger.Services;

public class AuthService(IUserService userService, IAppSettings settings) : IAuthService
{
    public async Task<string> AuthenticateTelegramAsync(
        long id,
        string? firstName,
        string? lastName,
        string? username,
        string? photoUrl,
        long authDate,
        string hash,
        CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = id.ToString(),
            ["auth_date"] = authDate.ToString()
        };
        if (!string.IsNullOrEmpty(firstName)) fields["first_name"] = firstName;
        if (!string.IsNullOrEmpty(lastName)) fields["last_name"] = lastName;
        if (!string.IsNullOrEmpty(username)) fields["username"] = username;
        if (!string.IsNullOrEmpty(photoUrl)) fields["photo_url"] = photoUrl;

        if (!ValidateTelegramHash(fields, hash))
            throw new UnauthorizedAccessException("Invalid Telegram hash");

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authDate > 86400)
            throw new UnauthorizedAccessException("Auth data expired");

        var user = await userService.GetOrCreateByTelegramAsync(id, username, firstName, ct);
        return GenerateJwt(user.Id, user.DisplayName ?? user.FirstName ?? "User");
    }

    public async Task<string> AuthenticateTelegramWebAppAsync(string initData, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(initData))
            throw new UnauthorizedAccessException("Empty initData");
        if (string.IsNullOrEmpty(settings.TelegramBotToken))
            throw new UnauthorizedAccessException("Bot token not configured");

        var fields = ParseQueryString(initData);
        if (!fields.TryGetValue("hash", out var hash) || string.IsNullOrEmpty(hash))
            throw new UnauthorizedAccessException("Missing hash");

        if (!ValidateWebAppHash(fields, hash))
            throw new UnauthorizedAccessException("Invalid WebApp hash");

        if (!fields.TryGetValue("auth_date", out var authDateRaw)
            || !long.TryParse(authDateRaw, out var authDate)
            || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authDate > 86400)
            throw new UnauthorizedAccessException("Auth data expired");

        if (!fields.TryGetValue("user", out var userJson) || string.IsNullOrWhiteSpace(userJson))
            throw new UnauthorizedAccessException("Missing user");

        using var doc = JsonDocument.Parse(userJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("id", out var idEl))
            throw new UnauthorizedAccessException("Missing user id");

        var id = idEl.GetInt64();
        var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
        var firstName = root.TryGetProperty("first_name", out var f) ? f.GetString() : null;

        var user = await userService.GetOrCreateByTelegramAsync(id, username, firstName, ct);
        return GenerateJwt(user.Id, user.DisplayName ?? user.FirstName ?? "User");
    }

    public async Task<string> AuthenticateByTelegramUserAsync(long telegramUserId, string? username, string? firstName, CancellationToken ct = default)
    {
        var user = await userService.GetOrCreateByTelegramAsync(telegramUserId, username, firstName, ct);
        return GenerateJwt(user.Id, user.DisplayName ?? user.FirstName ?? "User");
    }

    public bool ValidateTelegramHash(Dictionary<string, string> fields, string hash)
    {
        if (string.IsNullOrEmpty(settings.TelegramBotToken)) return false;

        var dataCheckString = string.Join('\n',
            fields.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(settings.TelegramBotToken));
        using var hmac = new HMACSHA256(secretKey);
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();
        return computedHex == hash.ToLowerInvariant();
    }

    private bool ValidateWebAppHash(Dictionary<string, string> fields, string hash)
    {
        var dataCheckString = string.Join('\n',
            fields.Where(kv => kv.Key != "hash")
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"));

        // secret_key = HMAC_SHA256(key="WebAppData", msg=bot_token)
        using var secretHmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = secretHmac.ComputeHash(Encoding.UTF8.GetBytes(settings.TelegramBotToken));

        using var dataHmac = new HMACSHA256(secretKey);
        var computed = dataHmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();
        return computedHex == hash.ToLowerInvariant();
    }

    private static Dictionary<string, string> ParseQueryString(string initData)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var key = Uri.UnescapeDataString(part[..eq]);
            var value = Uri.UnescapeDataString(part[(eq + 1)..]);
            result[key] = value;
        }
        return result;
    }

    private string GenerateJwt(Guid userId, string name)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, name)
        };
        var token = new JwtSecurityToken(
            settings.JwtIssuer,
            settings.JwtIssuer,
            claims,
            expires: DateTime.UtcNow.AddHours(settings.JwtExpiryHours),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
