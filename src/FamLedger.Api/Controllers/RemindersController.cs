using FamLedger.Api.Extensions;
using FamLedger.Common;
using FamLedger.Domain.Enums;
using FamLedger.Domain.Models;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Api.Controllers;

[ApiController]
[Route("api/reminders")]
[Authorize]
public class RemindersController(
    AppDbContext db,
    IUserService userService,
    IReminderService reminderService) : ControllerBase
{
    public record ReminderDayRequest(int Day, bool Enabled, string? TimeUtc);

    public record ReminderScheduleRequest(bool SameTimeEveryDay, ReminderDayRequest[]? Days);

    public record ReminderRequest(
        string? Message,
        string? TimeUtc,
        string Audience,
        bool? IsEnabled,
        string? Kind,
        int[]? ThresholdPercents,
        ReminderScheduleRequest? Schedule);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var (context, userId) = await GetActiveContextAsync(ct);
        await reminderService.EnsureDefaultsAsync(context.Id, userId, context.IsPersonal, ct);
        var items = await reminderService.ListVisibleAsync(context.Id, userId, ct);
        return Ok(items.Select(r => ToDto(r, userId)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReminderRequest request, CancellationToken ct)
    {
        try
        {
            var (context, userId) = await GetActiveContextAsync(ct);
            if (!TryParseAudience(request.Audience, out var audience))
                return BadRequest(new { message = "Invalid audience" });

            if (!TryParseTimeField(request.TimeUtc, out var timeUtc, out var timeError))
                return BadRequest(new { message = timeError });
            if (!TryParseSchedule(request.Schedule, out var schedule, out var scheduleError))
                return BadRequest(new { message = scheduleError });

            var reminder = await reminderService.CreateAsync(
                context.Id,
                userId,
                request.Message,
                timeUtc,
                audience,
                ReminderKind.Custom,
                request.ThresholdPercents,
                context.IsPersonal,
                schedule,
                ct);

            var created = await db.Reminders
                .AsNoTracking()
                .Include(r => r.CreatedByUser)
                .FirstAsync(r => r.Id == reminder.Id, ct);
            return Ok(ToDto(created, userId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ReminderRequest request, CancellationToken ct)
    {
        try
        {
            var (context, userId) = await GetActiveContextAsync(ct);
            if (!TryParseAudience(request.Audience, out var audience))
                return BadRequest(new { message = "Invalid audience" });

            if (!TryParseTimeField(request.TimeUtc, out var timeUtc, out var timeError))
                return BadRequest(new { message = timeError });
            if (!TryParseSchedule(request.Schedule, out var schedule, out var scheduleError))
                return BadRequest(new { message = scheduleError });

            await reminderService.UpdateAsync(
                id,
                userId,
                request.Message,
                timeUtc,
                audience,
                request.IsEnabled ?? true,
                request.ThresholdPercents,
                context.IsPersonal,
                schedule,
                ct);

            var updated = await db.Reminders
                .AsNoTracking()
                .Include(r => r.CreatedByUser)
                .FirstAsync(r => r.Id == id, ct);
            return Ok(ToDto(updated, userId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var (_, userId) = await GetActiveContextAsync(ct);
            await reminderService.DeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<(Domain.Entities.BudgetContext Context, Guid UserId)> GetActiveContextAsync(CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct)
            ?? throw new InvalidOperationException("User not found");
        if (user.ActiveContextId is null) throw new InvalidOperationException("No active context");
        var context = await db.BudgetContexts.FindAsync([user.ActiveContextId.Value], ct)
            ?? throw new InvalidOperationException("Context not found");
        return (context, user.Id);
    }

    private static object ToDto(Domain.Entities.Reminder r, Guid currentUserId) => new
    {
        r.Id,
        Kind = r.Kind.ToString(),
        r.Message,
        TimeUtc = r.TimeUtc?.ToString("HH:mm"),
        Schedule = ToScheduleDto(r),
        ThresholdPercents = r.ThresholdPercents ?? [],
        Audience = r.Audience.ToString(),
        r.IsEnabled,
        CreatedByUserId = r.CreatedByUserId,
        CreatedByName = r.CreatedByUser.DisplayName ?? r.CreatedByUser.FirstName ?? r.CreatedByUser.Username,
        CanEdit = r.CreatedByUserId == currentUserId,
        IsStandard = r.Kind != ReminderKind.Custom,
        CreatedAtUtc = r.CreatedAtUtc,
        UpdatedAtUtc = r.UpdatedAtUtc,
    };

    private static object? ToScheduleDto(Domain.Entities.Reminder reminder)
    {
        var schedule = ReminderScheduleRules.Resolve(reminder.ScheduleJson, reminder.TimeUtc);
        if (schedule is null)
            return null;

        return new
        {
            schedule.SameTimeEveryDay,
            Days = schedule.Days
                .Where(day => day.Enabled)
                .Select(day => new
                {
                    day.Day,
                    day.Enabled,
                    TimeUtc = day.TimeUtc.ToString("HH:mm"),
                }),
        };
    }

    private static bool TryParseTimeField(string? value, out TimeOnly? time, out string? error)
    {
        time = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (!TimeOnly.TryParse(value, out var parsed))
        {
            error = "Invalid timeUtc, expected HH:mm";
            return false;
        }

        time = new TimeOnly(parsed.Hour, parsed.Minute);
        return true;
    }

    private static bool TryParseSchedule(
        ReminderScheduleRequest? request,
        out ReminderSchedule? schedule,
        out string? error)
    {
        schedule = null;
        error = null;
        if (request is null)
            return true;

        var days = new List<ReminderDaySlot>();
        foreach (var day in request.Days ?? [])
        {
            if (!day.Enabled)
                continue;
            if (string.IsNullOrWhiteSpace(day.TimeUtc) || !TimeOnly.TryParse(day.TimeUtc, out var parsed))
            {
                error = "Invalid timeUtc, expected HH:mm";
                return false;
            }

            days.Add(new ReminderDaySlot
            {
                Day = day.Day,
                Enabled = true,
                TimeUtc = new TimeOnly(parsed.Hour, parsed.Minute),
            });
        }

        schedule = new ReminderSchedule
        {
            SameTimeEveryDay = request.SameTimeEveryDay,
            Days = days,
        };
        return true;
    }

    private static bool TryParseAudience(string? value, out ReminderAudience audience)
    {
        audience = ReminderAudience.Self;
        return Enum.TryParse(value, ignoreCase: true, out audience);
    }
}
