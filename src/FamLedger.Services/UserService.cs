using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FamLedger.Services;

public class UserService(AppDbContext db, IRedisService redis) : IUserService
{
    public async Task<User> GetOrCreateByTelegramAsync(long telegramId, string? username, string? firstName, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramId, ct);
        if (user is not null)
        {
            user.Username = username ?? user.Username;
            user.FirstName = firstName ?? user.FirstName;
            await db.SaveChangesAsync(ct);
            return user;
        }

        user = new User
        {
            TelegramUserId = telegramId,
            Username = username,
            FirstName = firstName,
            DisplayName = firstName
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var personal = await CreatePersonalContextInternalAsync(user, ct);
        user.ActiveContextId = personal.Id;
        await db.SaveChangesAsync(ct);
        return user;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User> UpdateProfileAsync(Guid userId, string? displayName, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([userId], ct) ?? throw new InvalidOperationException("User not found");
        user.DisplayName = displayName;
        await db.SaveChangesAsync(ct);
        await redis.DeleteAsync(CacheKeys.UserProfile(userId));
        return user;
    }

    public async Task<string?> ReplaceAvatarKeyAsync(Guid userId, string avatarKey, CancellationToken ct = default)
    {
        while (true)
        {
            var user = await db.Users.Where(user => user.Id == userId)
                .Select(user => new { user.AvatarKey }).SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("User not found");
            var updated = await db.Users.Where(row => row.Id == userId && row.AvatarKey == user.AvatarKey)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AvatarKey, avatarKey), ct);
            if (updated == 1)
            {
                await redis.DeleteAsync(CacheKeys.UserProfile(userId));
                return user.AvatarKey;
            }
        }
    }

    public async Task<User> SetActiveContextAsync(Guid userId, Guid contextId, CancellationToken ct = default)
    {
        var member = await db.ContextMembers.AnyAsync(m => m.UserId == userId && m.ContextId == contextId, ct);
        if (!member) throw new UnauthorizedAccessException("Not a member of context");

        var user = await db.Users.FindAsync([userId], ct) ?? throw new InvalidOperationException("User not found");
        user.ActiveContextId = contextId;
        await db.SaveChangesAsync(ct);
        return user;
    }

    private async Task<BudgetContext> CreatePersonalContextInternalAsync(User user, CancellationToken ct)
    {
        var context = new BudgetContext
        {
            Name = "Личный бюджет",
            IsPersonal = true,
            InviteCode = InviteCodeGenerator.Generate()
        };
        db.BudgetContexts.Add(context);
        db.ContextMembers.Add(new ContextMember
        {
            ContextId = context.Id,
            UserId = user.Id,
            Role = FamilyMemberRole.Head
        });
        foreach (var (name, kind, order) in DefaultCategories.Items)
        {
            db.Categories.Add(new Category
            {
                ContextId = context.Id,
                Name = name,
                Kind = kind,
                SortOrder = order,
                IsDefault = true
            });
        }
        await db.SaveChangesAsync(ct);
        return context;
    }
}
