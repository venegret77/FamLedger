using System.Security.Claims;
using FamLedger.Api.Extensions;
using FamLedger.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamLedger.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ILoginTokenService loginTokenService) : ControllerBase
{
    public record TelegramAuthRequest(
        long Id,
        string? FirstName,
        string? LastName,
        string? Username,
        string? PhotoUrl,
        long AuthDate,
        string Hash);

    public record BotLoginRequest(string Token);
    public record WebAppLoginRequest(string InitData);

    [HttpPost("telegram")]
    public async Task<IActionResult> TelegramLogin([FromBody] TelegramAuthRequest request, CancellationToken ct)
    {
        try
        {
            var token = await authService.AuthenticateTelegramAsync(
                request.Id,
                request.FirstName,
                request.LastName,
                request.Username,
                request.PhotoUrl,
                request.AuthDate,
                request.Hash,
                ct);

            AppendAuthCookie(token);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("webapp")]
    public async Task<IActionResult> WebAppLogin([FromBody] WebAppLoginRequest request, CancellationToken ct)
    {
        try
        {
            var token = await authService.AuthenticateTelegramWebAppAsync(request.InitData, ct);
            AppendAuthCookie(token);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("bot")]
    public async Task<IActionResult> BotLogin([FromBody] BotLoginRequest request, CancellationToken ct)
    {
        var telegramId = await loginTokenService.ConsumeAsync(request.Token, ct);
        if (telegramId is null)
            return Unauthorized(new { message = "Код недействителен или уже использован. Запроси новый в боте (/start login)." });

        var jwt = await authService.AuthenticateByTelegramUserAsync(telegramId.Value, null, null, ct);
        AppendAuthCookie(jwt);
        return Ok(new { success = true });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("auth_token", AuthCookieOptions());
        return Ok();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me([FromServices] IUserService userService, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        if (user is null) return NotFound();
        return Ok(new
        {
            user.Id,
            user.TelegramUserId,
            user.DisplayName,
            user.FirstName,
            user.Username,
            user.AvatarKey,
            user.ActiveContextId
        });
    }

    private void AppendAuthCookie(string token)
    {
        var options = AuthCookieOptions();
        options.MaxAge = TimeSpan.FromDays(7);
        Response.Cookies.Append("auth_token", token, options);
    }

    private CookieOptions AuthCookieOptions()
    {
        var forwardedProto = Request.Headers["X-Forwarded-Proto"].ToString();
        var secure = Request.IsHttps ||
                     string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
        };
    }
}
