using FamLedger.Api.Extensions;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamLedger.Api.Controllers;

[ApiController]
[Route("api/category-limits")]
[Authorize]
public class CategoryLimitsController(
    AppDbContext db,
    IUserService userService,
    ICategorySpendingLimitService limitService) : ControllerBase
{
    public record CategoryLimitRequest(
        Guid? CategoryId,
        decimal LimitAmount,
        int[]? ThresholdPercents,
        string Audience,
        bool? IsEnabled);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var (context, userId) = await GetActiveContextAsync(ct);
        var items = await limitService.ListAsync(context.Id, userId, ct);
        return Ok(items.Select(l => ToDto(l, userId)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryLimitRequest request, CancellationToken ct)
    {
        try
        {
            var (context, userId) = await GetActiveContextAsync(ct);
            if (request.CategoryId is null)
                return BadRequest(new { message = "CategoryId is required" });
            if (!TryParseAudience(request.Audience, out var audience))
                return BadRequest(new { message = "Invalid audience" });

            var created = await limitService.CreateAsync(
                context.Id,
                userId,
                request.CategoryId.Value,
                request.LimitAmount,
                request.ThresholdPercents,
                audience,
                context.IsPersonal,
                ct);
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
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryLimitRequest request, CancellationToken ct)
    {
        try
        {
            var (context, userId) = await GetActiveContextAsync(ct);
            if (!TryParseAudience(request.Audience, out var audience))
                return BadRequest(new { message = "Invalid audience" });

            var updated = await limitService.UpdateAsync(
                id,
                userId,
                request.LimitAmount,
                request.ThresholdPercents,
                audience,
                request.IsEnabled ?? true,
                context.IsPersonal,
                ct);
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
            await limitService.DeleteAsync(id, userId, ct);
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

    private static object ToDto(Domain.Entities.CategorySpendingLimit l, Guid currentUserId) => new
    {
        l.Id,
        l.CategoryId,
        CategoryName = l.Category.Name,
        l.LimitAmount,
        ThresholdPercents = l.ThresholdPercents ?? [],
        Audience = l.Audience.ToString(),
        l.IsEnabled,
        CreatedByUserId = l.CreatedByUserId,
        CreatedByName = l.CreatedByUser.DisplayName ?? l.CreatedByUser.FirstName ?? l.CreatedByUser.Username,
        CanEdit = l.CreatedByUserId == currentUserId,
        CreatedAtUtc = l.CreatedAtUtc,
        UpdatedAtUtc = l.UpdatedAtUtc,
    };

    private static bool TryParseAudience(string? value, out ReminderAudience audience)
    {
        audience = ReminderAudience.Self;
        return Enum.TryParse(value, ignoreCase: true, out audience);
    }
}
