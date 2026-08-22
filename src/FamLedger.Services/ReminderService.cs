using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class ReminderService(AppDbContext db) : IReminderService
{
    private static readonly ReminderKind[] StandardKinds =
    [
        ReminderKind.DailyBalance,
        ReminderKind.BudgetAlert,
        ReminderKind.EveningCheckIn,
        ReminderKind.PeriodEnding,
        ReminderKind.UnpaidDebts,
        ReminderKind.UnpaidPlanned,
    ];

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
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.TimeUtc)
            .ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task EnsureDefaultsAsync(
        Guid contextId,
        Guid userId,
        bool isPersonalContext,
        CancellationToken ct = default)
    {
        var existing = await db.Reminders
            .Where(r => r.ContextId == contextId && r.CreatedByUserId == userId && r.Kind != ReminderKind.Custom)
            .Select(r => r.Kind)
            .ToListAsync(ct);

        var audience = isPersonalContext ? ReminderAudience.Self : ReminderAudience.Self;
        var now = DateTime.UtcNow;
        var added = false;

        foreach (var kind in StandardKinds)
        {
            if (existing.Contains(kind)) continue;

            db.Reminders.Add(new Reminder
            {
                ContextId = contextId,
                CreatedByUserId = userId,
                Kind = kind,
                Message = null,
                TimeUtc = DefaultTimeUtc(kind),
                ThresholdPercent = kind == ReminderKind.BudgetAlert ? 80 : null,
                Audience = audience,
                IsEnabled = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
            added = true;
        }

        if (added)
            await db.SaveChangesAsync(ct);
    }

    public async Task<Reminder> CreateAsync(
        Guid contextId,
        Guid userId,
        string? message,
        TimeOnly? timeUtc,
        ReminderAudience audience,
        ReminderKind kind,
        int? thresholdPercent,
        bool isPersonalContext,
        CancellationToken ct = default)
    {
        if (kind != ReminderKind.Custom)
            throw new InvalidOperationException("Standard reminders are created automatically");

        ValidateAudience(audience, isPersonalContext);
        var trimmed = ValidateMessage(message, required: true);
        if (timeUtc is null)
            throw new InvalidOperationException("Time is required");

        var reminder = new Reminder
        {
            ContextId = contextId,
            CreatedByUserId = userId,
            Kind = ReminderKind.Custom,
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
        string? message,
        TimeOnly? timeUtc,
        ReminderAudience audience,
        bool isEnabled,
        int? thresholdPercent,
        bool isPersonalContext,
        CancellationToken ct = default)
    {
        ValidateAudience(audience, isPersonalContext);

        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException("Reminder not found");
        if (reminder.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("Only the creator can edit this reminder");

        if (reminder.Kind == ReminderKind.Custom)
        {
            reminder.Message = ValidateMessage(message, required: true);
            if (timeUtc is null)
                throw new InvalidOperationException("Time is required");
            reminder.TimeUtc = timeUtc;
        }
        else
        {
            if (NeedsTime(reminder.Kind))
            {
                if (timeUtc is null)
                    throw new InvalidOperationException("Time is required");
                reminder.TimeUtc = timeUtc;
            }

            if (reminder.Kind == ReminderKind.BudgetAlert)
                reminder.ThresholdPercent = thresholdPercent is > 0 and <= 100
                    ? thresholdPercent
                    : reminder.ThresholdPercent ?? 80;

            if (reminder.Kind == ReminderKind.EveningCheckIn && !string.IsNullOrWhiteSpace(message))
                reminder.Message = ValidateMessage(message, required: false);
        }

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
        if (reminder.Kind != ReminderKind.Custom)
            throw new InvalidOperationException("Standard reminders cannot be deleted; disable them instead");

        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Reminder>> GetDueTimedAsync(
        TimeOnly timeUtc,
        DateOnly todayUtc,
        CancellationToken ct = default)
    {
        return await db.Reminders
            .Include(r => r.CreatedByUser)
            .Include(r => r.Context)
            .Where(r =>
                r.IsEnabled &&
                r.TimeUtc != null &&
                r.TimeUtc.Value.Hour == timeUtc.Hour &&
                r.TimeUtc.Value.Minute == timeUtc.Minute &&
                (r.LastFiredDateUtc == null || r.LastFiredDateUtc != todayUtc) &&
                (r.Kind == ReminderKind.Custom
                 || r.Kind == ReminderKind.DailyBalance
                 || r.Kind == ReminderKind.EveningCheckIn
                 || r.Kind == ReminderKind.PeriodEnding
                 || r.Kind == ReminderKind.UnpaidDebts
                 || r.Kind == ReminderKind.UnpaidPlanned))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Reminder>> GetEnabledBudgetAlertsAsync(
        Guid contextId,
        CancellationToken ct = default)
    {
        return await db.Reminders
            .Include(r => r.CreatedByUser)
            .Include(r => r.Context)
            .Where(r =>
                r.IsEnabled &&
                r.Kind == ReminderKind.BudgetAlert &&
                r.ContextId == contextId)
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

    private static TimeOnly? DefaultTimeUtc(ReminderKind kind) => kind switch
    {
        ReminderKind.DailyBalance => new TimeOnly(18, 0),
        ReminderKind.EveningCheckIn => new TimeOnly(19, 0),
        ReminderKind.UnpaidDebts => new TimeOnly(10, 0),
        ReminderKind.UnpaidPlanned => new TimeOnly(17, 0),
        ReminderKind.BudgetAlert => null,
        ReminderKind.PeriodEnding => new TimeOnly(9, 0),
        _ => new TimeOnly(12, 0),
    };

    private static bool NeedsTime(ReminderKind kind) =>
        kind is ReminderKind.Custom or ReminderKind.DailyBalance
            or ReminderKind.EveningCheckIn or ReminderKind.UnpaidDebts
            or ReminderKind.UnpaidPlanned or ReminderKind.PeriodEnding;

    private static void ValidateAudience(ReminderAudience audience, bool isPersonalContext)
    {
        if (audience == ReminderAudience.Family && isPersonalContext)
            throw new InvalidOperationException("Family audience is only available in a family budget");
    }

    private static string? ValidateMessage(string? message, bool required)
    {
        var trimmed = message?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (required)
                throw new InvalidOperationException("Message is required");
            return null;
        }

        if (trimmed.Length > 1000)
            throw new InvalidOperationException("Message is too long");
        return trimmed;
    }
}
