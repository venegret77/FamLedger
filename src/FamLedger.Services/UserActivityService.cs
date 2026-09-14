using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class UserActivityService(AppDbContext db) : IUserActivityService
{
    public async Task TouchAsync(
        Guid userId,
        Guid contextId,
        UserActivityKind kind,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var state = await db.UserActivityStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ContextId == contextId, ct);

        if (state is null)
        {
            state = new UserActivityState
            {
                UserId = userId,
                ContextId = contextId,
                LastActivityAtUtc = now,
                LastActivityKind = kind,
                LastRecordedAtUtc = kind == UserActivityKind.Transaction ? now : null,
            };
            db.UserActivityStates.Add(state);
        }
        else
        {
            state.LastActivityAtUtc = now;
            state.LastActivityKind = kind;
            if (kind == UserActivityKind.Transaction)
                state.LastRecordedAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> WasActiveWithinAsync(
        Guid userId,
        Guid contextId,
        TimeSpan window,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - window;
        return await db.UserActivityStates
            .AsNoTracking()
            .AnyAsync(
                s => s.UserId == userId
                     && s.ContextId == contextId
                     && s.LastActivityAtUtc >= cutoff,
                ct);
    }

    public async Task<bool> WasRecordedWithinAsync(
        Guid userId,
        Guid contextId,
        TimeSpan window,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - window;
        return await db.UserActivityStates
            .AsNoTracking()
            .AnyAsync(
                s => s.UserId == userId
                     && s.ContextId == contextId
                     && s.LastRecordedAtUtc != null
                     && s.LastRecordedAtUtc >= cutoff,
                ct);
    }
}
