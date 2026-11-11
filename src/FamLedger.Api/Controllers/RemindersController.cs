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
    public record ReminderDayRequest(int Day, bool Enabled, string? TimeUtc, string? TimeLocal);

    public record ReminderScheduleRequest(bool SameTimeEveryDay, ReminderDayRequest[]? Days, string? TimeZoneId);

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
        try
        {
            var (context, userId) = await GetActiveContextAsync(ct);
            await reminderService.EnsureDefaultsAsync(context.Id, userId, context.IsPersonal, ct);
            var items = await reminderService.ListVisibleAsync(context.Id, userId, ct);
            return Ok(items.Select(r => ToDto(r, userId)));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
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
            if (!await db.Reminders.AnyAsync(reminder => reminder.Id == id && reminder.ContextId == context.Id, ct))
                return NotFound();
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
            var (context, userId) = await GetActiveContextAsync(ct);
            if (!await db.Reminders.AnyAsync(reminder => reminder.Id == id && reminder.ContextId == context.Id, ct))
                return NotFound();
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
        var context = await db.BudgetContexts.FirstOrDefaultAsync(
            context => context.Id == user.ActiveContextId && context.Members.Any(member => member.UserId == user.Id), ct)
            ?? throw new UnauthorizedAccessException();
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
            schedule.TimeZoneId,
            Days = schedule.Days
                .Where(day => day.Enabled)
                .Select(day => new
                {
                    day.Day,
                    day.Enabled,
                    TimeUtc = schedule.TimeZoneId is null ? day.TimeUtc.ToString("HH:mm") : null,
                    TimeLocal = day.TimeLocal?.ToString("HH:mm"),
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
            var time = request.TimeZoneId is null ? day.TimeUtc : day.TimeLocal;
            if (!TimeOnly.TryParseExact(time, "HH:mm", out var parsed))
            {
                error = "Invalid schedule time, expected HH:mm";
                return false;
            }

            days.Add(new ReminderDaySlot
            {
                Day = day.Day,
                Enabled = true,
                TimeUtc = request.TimeZoneId is null ? parsed : default,
                TimeLocal = request.TimeZoneId is null ? null : parsed,
            });
        }

        schedule = new ReminderSchedule
        {
            SameTimeEveryDay = request.SameTimeEveryDay,
            TimeZoneId = request.TimeZoneId,
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
