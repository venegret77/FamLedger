using FamLedger.Api.Extensions;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamLedger.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    public record WebPushSubscribeRequest(string Endpoint, string P256dh, string Auth);

    [Authorize]
    [HttpPost("webpush/subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] WebPushSubscribeRequest request, CancellationToken ct)
    {
        await notificationService.SubscribeWebPushAsync(User.GetUserId(), request.Endpoint, request.P256dh, request.Auth, ct);
        return Ok();
    }
}

[ApiController]
[Route("api/webhooks")]
[Authorize]
public class WebhooksController(AppDbContext db) : ControllerBase
{
    public record WebhookRequest(string Url);

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] WebhookRequest request, CancellationToken ct)
    {
        var endpoint = new Domain.Entities.WebhookEndpoint
        {
            UserId = User.GetUserId(),
            Url = request.Url,
            Secret = Guid.NewGuid().ToString("N")
        };
        db.WebhookEndpoints.Add(endpoint);
        await db.SaveChangesAsync(ct);
        return Ok(new { endpoint.Id, endpoint.Secret });
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = db.WebhookEndpoints.Where(w => w.UserId == User.GetUserId()).Select(w => new { w.Id, w.Url, w.IsActive, w.CreatedAt });
        return Ok(await Task.FromResult(list.ToList()));
    }
}
