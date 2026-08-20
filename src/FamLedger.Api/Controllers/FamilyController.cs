using FamLedger.Api.Extensions;
using FamLedger.Common;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Api.Controllers;

[ApiController]
[Authorize]
public class MeAliasController(IUserService userService, AppDbContext db, IFileStorageService fileStorage) : ControllerBase
{
    [HttpGet("/api/me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        if (user is null) return NotFound();
        string? activeContextName = null;
        if (user.ActiveContextId is not null)
        {
            var context = await db.BudgetContexts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == user.ActiveContextId.Value, ct);
            activeContextName = context?.Name;
        }
        var avatarUrl = await fileStorage.GetAvatarUrlAsync(user.AvatarKey, ct);
        return Ok(new
        {
            user.Id,
            user.TelegramUserId,
            user.DisplayName,
            user.FirstName,
            user.Username,
            user.AvatarKey,
            avatarUrl,
            user.ActiveContextId,
            activeContextName
        });
    }
}

[ApiController]
[Route("api/family")]
[Authorize]
public class FamilyController(
    IUserService userService,
    IContextService contextService,
    AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        if (user?.ActiveContextId is null)
            return Ok(new { isPersonal = true, contextName = (string?)null, inviteCode = (string?)null, members = Array.Empty<object>(), joinRequests = Array.Empty<object>() });

        var context = await contextService.GetByIdAsync(user.ActiveContextId.Value, ct);
        if (context is null || context.IsPersonal)
            return Ok(new { isPersonal = true, contextName = (string?)null, inviteCode = (string?)null, members = Array.Empty<object>(), joinRequests = Array.Empty<object>() });

        var members = await contextService.GetMembersAsync(context.Id, ct);
        var requests = await contextService.GetPendingRequestsAsync(context.Id, ct);
        var membership = await contextService.GetMembershipAsync(context.Id, user.Id, ct);

        return Ok(new
        {
            isPersonal = false,
            contextName = context.Name,
            inviteCode = context.InviteCode,
            context.PeriodStartDay,
            context.BaseCurrency,
            myRole = membership?.Role,
            members = members.Select(m => new
            {
                m.Id,
                m.UserId,
                displayName = m.User.DisplayName ?? m.User.FirstName ?? "User",
                username = m.User.Username,
                m.Role,
                m.JoinedAt
            }),
            joinRequests = requests.Select(r => new
            {
                r.Id,
                r.UserId,
                displayName = r.User.DisplayName ?? r.User.FirstName ?? "User",
                username = r.User.Username,
                status = r.Status.ToString(),
                r.CreatedAt
            })
        });
    }

    [HttpPost("join-requests/{requestId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid requestId, CancellationToken ct)
    {
        await contextService.ApproveJoinAsync(requestId, User.GetUserId(), ct);
        return Ok();
    }

    [HttpPost("join-requests/{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid requestId, CancellationToken ct)
    {
        await contextService.RejectJoinAsync(requestId, User.GetUserId(), ct);
        return Ok();
    }

    [HttpPatch("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] ContextsController.UpdateSettingsRequest request, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        if (user?.ActiveContextId is null) return BadRequest();
        await contextService.UpdateSettingsAsync(user.ActiveContextId.Value, request.PeriodStartDay, request.BaseCurrency, user.Id, ct);
        return Ok();
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        if (user?.ActiveContextId is null) return Ok(new { periodStartDay = 15, baseCurrency = "RSD" });
        var context = await db.BudgetContexts.FindAsync([user.ActiveContextId.Value], ct);
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.ContextId == context!.Id)
            .OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.Name, c.Kind })
            .ToListAsync(ct);
        return Ok(new
        {
            context!.PeriodStartDay,
            context.BaseCurrency,
            contextName = context.Name,
            isPersonal = context.IsPersonal,
            categories
        });
    }
}

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(IUserService userService, AppDbContext db, ICategoryService categoryService, IContextService contextService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        if (user?.ActiveContextId is null)
            return Ok(new { periodStartDay = 15, baseCurrency = "RSD", categories = Array.Empty<object>(), myRole = "Head", canManagePlan = true, canManageFamilySettings = true });

        var context = await db.BudgetContexts.FindAsync([user.ActiveContextId.Value], ct);
        var categories = await categoryService.GetByContextAsync(context!.Id, ct);
        var membership = await db.ContextMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ContextId == context.Id && m.UserId == user.Id, ct);
        var role = membership?.Role ?? Domain.Enums.FamilyMemberRole.Head;

        return Ok(new
        {
            context.PeriodStartDay,
            context.BaseCurrency,
            contextName = context.Name,
            isPersonal = context.IsPersonal,
            myRole = role.ToString(),
            canManagePlan = RolePermissions.CanManagePlan(role),
            canManageFamilySettings = RolePermissions.CanManageFamilySettings(role),
            categories = categories.Select(c => new { c.Id, c.Name, c.Kind })
        });
    }

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] ContextsController.UpdateSettingsRequest request, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        if (user?.ActiveContextId is null) return BadRequest();
        try
        {
            await contextService.UpdateSettingsAsync(user.ActiveContextId.Value, request.PeriodStartDay, request.BaseCurrency, user.Id, ct);
            return Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
