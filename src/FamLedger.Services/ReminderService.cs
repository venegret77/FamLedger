using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Domain.Models;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class ReminderService(AppDbContext db) : IReminderService
{
    private static readonly TimeSpan DeliveryWindow = TimeSpan.FromHours(2);
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
                ThresholdPercents = kind == ReminderKind.BudgetAlert
                    ? ThresholdPercentHelper.DefaultBudgetAlert
                    : [],
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
        IReadOnlyList<int>? thresholdPercents,
        bool isPersonalContext,
        ReminderSchedule? schedule = null,
        CancellationToken ct = default)
    {
        if (kind != ReminderKind.Custom)
            throw new InvalidOperationException("Standard reminders are created automatically");

        ValidateAudience(audience, isPersonalContext);
        var trimmed = ValidateMessage(message, required: true);
        var normalized = RequireSchedule(schedule, timeUtc);

        var reminder = new Reminder
        {
            ContextId = contextId,
            CreatedByUserId = userId,
            Kind = ReminderKind.Custom,
            Message = trimmed,
            TimeUtc = normalized.TimeZoneId is null ? ReminderScheduleRules.RepresentativeTime(normalized) : null,
            ScheduleJson = ReminderScheduleRules.Serialize(normalized),
            Audience = audience,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        reminder.NextDueAtUtc = NextOccurrence(reminder, reminder.CreatedAtUtc);
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
        IReadOnlyList<int>? thresholdPercents,
        bool isPersonalContext,
        ReminderSchedule? schedule = null,
        CancellationToken ct = default)
    {
        ValidateAudience(audience, isPersonalContext);

        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException("Reminder not found");
        if (reminder.CreatedByUserId != userId || !await IsCurrentMemberAsync(reminder, userId, ct))
            throw new UnauthorizedAccessException("Only the creator can edit this reminder");

        if (reminder.Kind == ReminderKind.Custom)
            reminder.Message = ValidateMessage(message, required: true);

        var previousSchedule = reminder.ScheduleJson;
        var previousTime = reminder.TimeUtc;
        if (NeedsTime(reminder.Kind))
            ApplySchedule(reminder, schedule, timeUtc);

        if (reminder.Kind != ReminderKind.Custom)
        {

            if (reminder.Kind == ReminderKind.BudgetAlert)
                reminder.ThresholdPercents = ThresholdPercentHelper.Normalize(
                    thresholdPercents, ThresholdPercentHelper.DefaultBudgetAlert);

            if (reminder.Kind == ReminderKind.EveningCheckIn && !string.IsNullOrWhiteSpace(message))
                reminder.Message = ValidateMessage(message, required: false);
        }

        var scheduleChanged = previousSchedule != reminder.ScheduleJson || previousTime != reminder.TimeUtc
            || reminder.IsEnabled != isEnabled || reminder.Audience != audience;
        reminder.Audience = audience;
        reminder.IsEnabled = isEnabled;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        if (scheduleChanged)
            reminder.NextDueAtUtc = isEnabled && NeedsTime(reminder.Kind)
                ? NextOccurrence(reminder, reminder.UpdatedAtUtc)
                : null;
        await db.SaveChangesAsync(ct);
        return reminder;
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException("Reminder not found");
        if (reminder.CreatedByUserId != userId || !await IsCurrentMemberAsync(reminder, userId, ct))
            throw new UnauthorizedAccessException("Only the creator can delete this reminder");
        if (reminder.Kind != ReminderKind.Custom)
            throw new InvalidOperationException("Standard reminders cannot be deleted; disable them instead");

        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync(ct);
    }

    private Task<bool> IsCurrentMemberAsync(Reminder reminder, Guid userId, CancellationToken ct) =>
        db.ContextMembers.AnyAsync(member => member.ContextId == reminder.ContextId && member.UserId == userId, ct);

    public async Task<IReadOnlyList<Reminder>> GetDueTimedAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var earliest = utcNow - DeliveryWindow;
        var timed = db.Reminders.Where(r => r.IsEnabled && r.Kind != ReminderKind.BudgetAlert
            && (r.ScheduleJson != null || r.TimeUtc != null));
        var unscheduled = await timed.AsNoTracking()
            .Where(r => r.NextDueAtUtc == null || r.NextDueAtUtc < earliest)
            .ToListAsync(ct);
        foreach (var reminder in unscheduled)
        {
            var after = earliest.AddTicks(-1);
            if (reminder.CreatedAtUtc > after) after = reminder.CreatedAtUtc;
            if (reminder.LastFiredAtUtc is { } firedAt && firedAt > after) after = firedAt;
            var next = NextOccurrence(reminder, after);
            if (next is null) continue;
            await timed.Where(r => r.Id == reminder.Id && r.NextDueAtUtc == reminder.NextDueAtUtc
                    && r.UpdatedAtUtc == reminder.UpdatedAtUtc)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.NextDueAtUtc, next), ct);
        }

        return await timed.AsNoTracking().Include(r => r.Context)
            .Where(r => r.NextDueAtUtc >= earliest && r.NextDueAtUtc <= utcNow)
            .OrderBy(r => r.NextDueAtUtc)
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

    public async Task CompleteTimedAsync(Reminder reminder, bool fired, CancellationToken ct = default)
    {
        if (reminder.NextDueAtUtc is not { } occurrence) return;
        var next = NextOccurrence(reminder, occurrence);
        var current = db.Reminders.Where(r => r.Id == reminder.Id && r.NextDueAtUtc == occurrence
            && r.IsEnabled && r.UpdatedAtUtc == reminder.UpdatedAtUtc);
        var occurrenceDate = DateOnly.FromDateTime(occurrence);
        await current.ExecuteUpdateAsync(setters => setters
            .SetProperty(r => r.NextDueAtUtc, next)
            .SetProperty(r => r.LastFiredAtUtc, r => fired ? occurrence : r.LastFiredAtUtc)
            .SetProperty(r => r.LastFiredDateUtc, r => fired ? occurrenceDate : r.LastFiredDateUtc), ct);
    }

    private static DateTime? NextOccurrence(Reminder reminder, DateTime after) =>
        ReminderScheduleRules.NextOccurrenceUtc(reminder.ScheduleJson, reminder.TimeUtc, after);

    public async Task<IReadOnlySet<int>> GetFiredThresholdsAsync(
        Guid reminderId,
        DateOnly todayUtc,
        CancellationToken ct = default)
    {
        var fired = await db.ReminderThresholdFires
            .AsNoTracking()
            .Where(f => f.ReminderId == reminderId && f.LastFiredDateUtc == todayUtc)
            .Select(f => f.ThresholdPercent)
            .ToListAsync(ct);
        return fired.ToHashSet();
    }

    public async Task MarkThresholdsFiredAsync(
        Guid reminderId,
        IEnumerable<int> thresholds,
        DateOnly todayUtc,
        CancellationToken ct = default)
    {
        var list = thresholds.Distinct().ToList();
        if (list.Count == 0) return;

        var existing = await db.ReminderThresholdFires
            .Where(f => f.ReminderId == reminderId && list.Contains(f.ThresholdPercent))
            .ToListAsync(ct);

        foreach (var threshold in list)
        {
            var row = existing.FirstOrDefault(f => f.ThresholdPercent == threshold);
            if (row is null)
            {
                db.ReminderThresholdFires.Add(new ReminderThresholdFire
                {
                    ReminderId = reminderId,
                    ThresholdPercent = threshold,
                    LastFiredDateUtc = todayUtc,
                });
            }
            else
            {
                row.LastFiredDateUtc = todayUtc;
            }
        }

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

    private static ReminderSchedule RequireSchedule(ReminderSchedule? schedule, TimeOnly? timeUtc)
    {
        if (schedule is not null)
            return ReminderScheduleRules.Normalize(schedule);
        if (timeUtc is null)
            throw new InvalidOperationException("Time is required");
        return ReminderScheduleRules.EveryDay(timeUtc.Value);
    }

    private static void ApplySchedule(Reminder reminder, ReminderSchedule? schedule, TimeOnly? timeUtc)
    {
        if (schedule is not null)
        {
            var normalized = ReminderScheduleRules.Normalize(schedule);
            reminder.ScheduleJson = ReminderScheduleRules.Serialize(normalized);
            reminder.TimeUtc = normalized.TimeZoneId is null ? ReminderScheduleRules.RepresentativeTime(normalized) : null;
            return;
        }

        if (timeUtc is null)
        {
            if (string.IsNullOrWhiteSpace(reminder.ScheduleJson) && reminder.TimeUtc is null)
                throw new InvalidOperationException("Time is required");
            return;
        }

        var existing = ReminderScheduleRules.Parse(reminder.ScheduleJson);
        if (existing is { TimeZoneId: not null } or { SameTimeEveryDay: false })
            return;

        if (existing is { SameTimeEveryDay: true })
        {
            foreach (var day in existing.Days)
                day.TimeUtc = timeUtc.Value;
            var normalized = ReminderScheduleRules.Normalize(existing);
            reminder.ScheduleJson = ReminderScheduleRules.Serialize(normalized);
            reminder.TimeUtc = timeUtc;
            return;
        }

        var everyDay = ReminderScheduleRules.EveryDay(timeUtc.Value);
        reminder.ScheduleJson = ReminderScheduleRules.Serialize(everyDay);
        reminder.TimeUtc = timeUtc;
    }

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
