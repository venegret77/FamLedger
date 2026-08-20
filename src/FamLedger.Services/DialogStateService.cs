using FamLedger.Common;
using FamLedger.Domain.Models;
using FamLedger.Interfaces.Services;

namespace FamLedger.Services;

public class DialogStateService(IRedisService redis) : IDialogStateService
{
    public Task<DialogState?> GetAsync(long chatId, CancellationToken ct = default) =>
        redis.GetObjectAsync<DialogState>(CacheKeys.BotDialog(chatId));

    public Task SetAsync(long chatId, DialogState state, TimeSpan? expiry, CancellationToken ct = default) =>
        redis.SetObjectAsync(CacheKeys.BotDialog(chatId), state, expiry ?? TimeSpan.FromHours(1));

    public Task ClearAsync(long chatId, CancellationToken ct = default) =>
        redis.DeleteAsync(CacheKeys.BotDialog(chatId));
}
