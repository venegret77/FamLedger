using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class ReminderService(AppDbContext db) : IReminderService
{
    public async Task<IReadOnlyList<Reminder>> ListVisibleAsync(
        Guid contextId,
        Guid userId,
        CancellationToken ct = default)
    {
        return await db.Reminders
            .AsNoTracking()
            .Include(r => r.CreatedByUser)
            .Where(r =>
                r.ContextId == contextId &&
                (r.Audience == ReminderAudience.Family || r.CreatedByUserId == userId))
            .OrderBy(r => r.TimeUtc)
            .ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<Reminder> CreateAsync(
        Guid contextId,
        Guid userId,
        string message,
        TimeOnly timeUtc,
        ReminderAudience audience,
        bool isPersonalContext,
        CancellationToken ct = default)
    {
        ValidateAudience(audience, isPersonalContext);
        var trimmed = ValidateMessage(message);

        var reminder = new Reminder
        {
            ContextId = contextId,
            CreatedByUserId = userId,
            Message = trimmed,
            TimeUtc = timeUtc,
            Audience = audience,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(ct);
        return reminder;
    }

    public async Task<Reminder> UpdateAsync(
        Guid id,
        Guid userId,
        string message,
        TimeOnly timeUtc,
        ReminderAudience audience,
        bool isEnabled,
        bool isPersonalContext,
        CancellationToken ct = default)
    {
        ValidateAudience(audience, isPersonalContext);
        var trimmed = ValidateMessage(message);

        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException("Reminder not found");
        if (reminder.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("Only the creator can edit this reminder");

        reminder.Message = trimmed;
        reminder.TimeUtc = timeUtc;
        reminder.Audience = audience;
        reminder.IsEnabled = isEnabled;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return reminder;
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException("Reminder not found");
        if (reminder.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("Only the creator can delete this reminder");

        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Reminder>> GetDueAsync(
        TimeOnly timeUtc,
        DateOnly todayUtc,
        CancellationToken ct = default)
    {
        return await db.Reminders
            .Include(r => r.CreatedByUser)
            .Where(r =>
                r.IsEnabled &&
                r.TimeUtc.Hour == timeUtc.Hour &&
                r.TimeUtc.Minute == timeUtc.Minute &&
                (r.LastFiredDateUtc == null || r.LastFiredDateUtc != todayUtc))
            .ToListAsync(ct);
    }

    public async Task MarkFiredAsync(Guid id, DateOnly todayUtc, CancellationToken ct = default)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (reminder is null) return;
        reminder.LastFiredDateUtc = todayUtc;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static void ValidateAudience(ReminderAudience audience, bool isPersonalContext)
    {
        if (audience == ReminderAudience.Family && isPersonalContext)
            throw new InvalidOperationException("Family audience is only available in a family budget");
    }

    private static string ValidateMessage(string message)
    {
        var trimmed = message.Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Message is required");
        if (trimmed.Length > 1000)
            throw new InvalidOperationException("Message is too long");
        return trimmed;
    }
}
