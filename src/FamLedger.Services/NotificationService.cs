using System.Net.Http.Json;
using System.Text.Json;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Interfaces.Settings;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace FamLedger.Services;

public class NotificationService(
    AppDbContext db,
    ITelegramBotClient? botClient,
    IHttpClientFactory httpClientFactory,
    IOptions<AppSettings> settings) : INotificationService
{
    public async Task SendTelegramAsync(long telegramUserId, string message, CancellationToken ct = default)
    {
        if (botClient is null) return;
        try
        {
            await botClient.SendMessage(telegramUserId, message, cancellationToken: ct);
        }
        catch
        {
            // user may have blocked bot
        }
    }

    public async Task NotifyContextMembersAsync(Guid contextId, string message, CancellationToken ct = default)
    {
        var userIds = await db.ContextMembers
            .Where(m => m.ContextId == contextId)
            .Select(m => m.User.TelegramUserId)
            .ToListAsync(ct);

        foreach (var tgId in userIds)
            await SendTelegramAsync(tgId, message, ct);
    }

    public async Task SubscribeWebPushAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct = default)
    {
        var existing = await db.NotificationSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);
        if (existing is not null) return;

        db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            UserId = userId,
            Endpoint = endpoint,
            P256dh = p256dh,
            Auth = auth
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task SendWebPushAsync(Guid userId, string title, string body, CancellationToken ct = default)
    {
        var pushSettings = settings.Value;
        if (string.IsNullOrWhiteSpace(pushSettings.WebPushPublicKey) ||
            string.IsNullOrWhiteSpace(pushSettings.WebPushPrivateKey))
            return;

        var subscriptions = await db.NotificationSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);
        if (subscriptions.Count == 0) return;

        // Without WebPush library, notify via Telegram as fallback for subscribed users
        var user = await db.Users.FindAsync([userId], ct);
        if (user is not null)
            await SendTelegramAsync(user.TelegramUserId, $"{title}: {body}", ct);
    }

    public async Task DispatchWebhooksAsync(Guid userId, string eventType, object payload, CancellationToken ct = default)
    {
        var endpoints = await db.WebhookEndpoints
            .Where(w => w.UserId == userId && w.IsActive)
            .ToListAsync(ct);
        if (endpoints.Count == 0) return;

        var client = httpClientFactory.CreateClient("webhooks");
        var body = new { type = eventType, payload, at = DateTime.UtcNow };

        foreach (var endpoint in endpoints)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
                {
                    Content = JsonContent.Create(body)
                };
                request.Headers.TryAddWithoutValidation("X-FamLedger-Secret", endpoint.Secret);
                await client.SendAsync(request, ct);
            }
            catch
            {
                // ignore failed webhook delivery
            }
        }
    }
}
