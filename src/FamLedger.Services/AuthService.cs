using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
