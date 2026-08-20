using FamLedger.Domain.Entities;

namespace FamLedger.Interfaces.Services;

public interface IUserService
{
    Task<User> GetOrCreateByTelegramAsync(long telegramId, string? username, string? firstName, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User> UpdateProfileAsync(Guid userId, string? displayName, CancellationToken ct = default);
    Task<User> SetAvatarKeyAsync(Guid userId, string avatarKey, CancellationToken ct = default);
    Task<User> SetActiveContextAsync(Guid userId, Guid contextId, CancellationToken ct = default);
}
