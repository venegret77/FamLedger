using System.Text.Json;
using FamLedger.Interfaces.Services;
using StackExchange.Redis;

namespace FamLedger.Services;

public class RedisService(IConnectionMultiplexer multiplexer) : IRedisService
{
    private readonly IDatabase _db = multiplexer.GetDatabase();

    public Task SetAsync(string key, string value, TimeSpan? expiry = null) =>
        _db.StringSetAsync(key, value, expiry);

    public Task<bool> TrySetAsync(string key, string value, TimeSpan expiry) =>
        _db.StringSetAsync(key, value, expiry, when: When.NotExists);

    public async Task<string?> GetAndDeleteAsync(string key)
    {
        var value = await _db.StringGetDeleteAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public Task SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null) =>
        _db.StringSetAsync(key, JsonSerializer.Serialize(value), expiry);

    public async Task<string?> GetAsync(string key)
    {
        var val = await _db.StringGetAsync(key);
        return val.HasValue ? val.ToString() : null;
    }

    public async Task<T?> GetObjectAsync<T>(string key)
    {
        var val = await GetAsync(key);
        return val is null ? default : JsonSerializer.Deserialize<T>(val);
    }

    public Task DeleteAsync(string key) => _db.KeyDeleteAsync(key);

    public Task<bool> ExistsAsync(string key) => _db.KeyExistsAsync(key);
}
