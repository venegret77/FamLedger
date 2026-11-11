namespace FamLedger.Interfaces.Services;

public interface IRedisService
{
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
    Task SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<string?> GetAsync(string key);
    Task<string?> GetAndDeleteAsync(string key);
    Task<bool> TrySetAsync(string key, string value, TimeSpan expiry);
    Task<T?> GetObjectAsync<T>(string key);
    Task DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
}
