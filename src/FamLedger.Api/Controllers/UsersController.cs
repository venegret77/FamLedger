using FamLedger.Api.Extensions;
using FamLedger.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamLedger.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IUserService userService, IFileStorageService fileStorage) : ControllerBase
{
    public record UpdateProfileRequest(string? DisplayName);

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await userService.UpdateProfileAsync(User.GetUserId(), request.DisplayName, ct);
        return Ok(new { user.DisplayName });
    }

    [HttpPost("me/avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) return BadRequest("Empty file");
        await using var stream = file.OpenReadStream();
        var key = await fileStorage.UploadAvatarAsync(User.GetUserId(), stream, file.ContentType, ct);
        var user = await userService.SetAvatarKeyAsync(User.GetUserId(), key, ct);
        var url = await fileStorage.GetAvatarUrlAsync(user.AvatarKey, ct);
        return Ok(new { avatarUrl = url });
    }
}
