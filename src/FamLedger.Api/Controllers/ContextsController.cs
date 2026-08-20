using FamLedger.Api.Extensions;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamLedger.Api.Controllers;

[ApiController]
[Route("api/contexts")]
[Authorize]
public class ContextsController(IContextService contextService, IUserService userService) : ControllerBase
{
    public record CreateFamilyRequest(string Name);
    public record JoinRequest(string InviteCode);
    public record UpdateSettingsRequest(int PeriodStartDay, string BaseCurrency);
    public record UpdateRoleRequest(Guid MemberId, FamilyMemberRole Role);
    public record SwitchContextRequest(Guid ContextId);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var contexts = await contextService.GetUserContextsAsync(User.GetUserId(), ct);
        return Ok(contexts.Select(c => new { c.Id, c.Name, c.IsPersonal, c.PeriodStartDay, c.BaseCurrency, c.InviteCode }));
    }

    [HttpPost("personal")]
    public async Task<IActionResult> CreatePersonal(CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        var context = await contextService.CreatePersonalContextAsync(user!, ct);
        return Ok(new { context.Id, context.Name });
    }

    [HttpPost("family")]
    public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequest request, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct);
        var context = await contextService.CreateFamilyContextAsync(user!, request.Name, ct);
        await userService.SetActiveContextAsync(User.GetUserId(), context.Id, ct);
        return Ok(new { context.Id, context.Name, context.InviteCode });
    }

    [HttpPost("switch")]
    public async Task<IActionResult> Switch([FromBody] SwitchContextRequest request, CancellationToken ct)
    {
        await userService.SetActiveContextAsync(User.GetUserId(), request.ContextId, ct);
        return Ok();
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinRequest request, CancellationToken ct)
    {
        var joinRequest = await contextService.RequestJoinAsync(User.GetUserId(), request.InviteCode, ct);
        return Ok(new { joinRequest.Id, joinRequest.Status });
    }

    [HttpGet("{contextId:guid}/members")]
    public async Task<IActionResult> Members(Guid contextId, CancellationToken ct)
    {
        var members = await contextService.GetMembersAsync(contextId, ct);
        return Ok(members.Select(m => new { m.Id, m.UserId, m.Role, Name = m.User.DisplayName ?? m.User.FirstName }));
    }

    [HttpGet("{contextId:guid}/join-requests")]
    public async Task<IActionResult> JoinRequests(Guid contextId, CancellationToken ct)
    {
        var requests = await contextService.GetPendingRequestsAsync(contextId, ct);
        return Ok(requests.Select(r => new { r.Id, r.UserId, Name = r.User.DisplayName ?? r.User.FirstName, r.CreatedAt }));
    }

    public record ApproveJoinBody(FamilyMemberRole? Role);

    [HttpPost("join-requests/{requestId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid requestId, [FromBody] ApproveJoinBody? body, CancellationToken ct)
    {
        await contextService.ApproveJoinAsync(requestId, User.GetUserId(), body?.Role ?? FamilyMemberRole.Member, ct);
        return Ok();
    }

    [HttpPost("join-requests/{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid requestId, CancellationToken ct)
    {
        await contextService.RejectJoinAsync(requestId, User.GetUserId(), ct);
        return Ok();
    }

    [HttpPatch("{contextId:guid}/settings")]
    public async Task<IActionResult> UpdateSettings(Guid contextId, [FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        await contextService.UpdateSettingsAsync(contextId, request.PeriodStartDay, request.BaseCurrency, User.GetUserId(), ct);
        return Ok();
    }

    [HttpPost("{contextId:guid}/invite/regenerate")]
    public async Task<IActionResult> RegenerateInvite(Guid contextId, CancellationToken ct)
    {
        var code = await contextService.RegenerateInviteCodeAsync(contextId, User.GetUserId(), ct);
        return Ok(new { inviteCode = code });
    }

    [HttpPatch("{contextId:guid}/members/role")]
    public async Task<IActionResult> UpdateRole(Guid contextId, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        await contextService.UpdateMemberRoleAsync(contextId, request.MemberId, request.Role, User.GetUserId(), ct);
        return Ok();
    }

    [HttpDelete("{contextId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid contextId, Guid memberId, CancellationToken ct)
    {
        try
        {
            await contextService.RemoveMemberAsync(contextId, memberId, User.GetUserId(), ct);
            return Ok();
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
}
