using FamLedger.Api.Extensions;
using FamLedger.Domain.Enums;
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
    public record ReminderRequest(string Message, string TimeUtc, string Audience, bool? IsEnabled);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var (context, userId) = await GetActiveContextAsync(ct);
        var items = await reminderService.ListVisibleAsync(context.Id, userId, ct);
        return Ok(items.Select(r => ToDto(r, userId)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReminderRequest request, CancellationToken ct)
    {
        try
        {
            var (context, userId) = await GetActiveContextAsync(ct);
            if (!TryParseTime(request.TimeUtc, out var timeUtc))
                return BadRequest(new { message = "Invalid timeUtc, expected HH:mm" });
            if (!TryParseAudience(request.Audience, out var audience))
                return BadRequest(new { message = "Invalid audience" });

            var reminder = await reminderService.CreateAsync(
                context.Id,
                userId,
                request.Message,
                timeUtc,
                audience,
                context.IsPersonal,
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
            if (!TryParseTime(request.TimeUtc, out var timeUtc))
                return BadRequest(new { message = "Invalid timeUtc, expected HH:mm" });
            if (!TryParseAudience(request.Audience, out var audience))
                return BadRequest(new { message = "Invalid audience" });

            await reminderService.UpdateAsync(
                id,
                userId,
                request.Message,
                timeUtc,
                audience,
                request.IsEnabled ?? true,
                context.IsPersonal,
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
        r.Message,
        TimeUtc = r.TimeUtc.ToString("HH:mm"),
        Audience = r.Audience.ToString(),
        r.IsEnabled,
        CreatedByUserId = r.CreatedByUserId,
        CreatedByName = r.CreatedByUser.DisplayName ?? r.CreatedByUser.FirstName ?? r.CreatedByUser.Username,
        CanEdit = r.CreatedByUserId == currentUserId,
        CreatedAtUtc = r.CreatedAtUtc,
        UpdatedAtUtc = r.UpdatedAtUtc,
    };

    private static bool TryParseTime(string? value, out TimeOnly time)
    {
        time = default;
        return !string.IsNullOrWhiteSpace(value) && TimeOnly.TryParse(value, out time);
    }

    private static bool TryParseAudience(string? value, out ReminderAudience audience)
    {
        audience = ReminderAudience.Self;
        return Enum.TryParse(value, ignoreCase: true, out audience);
    }
}
