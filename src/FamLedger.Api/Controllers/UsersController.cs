using FamLedger.Api.Extensions;
using FamLedger.Interfaces.Services;
using FamLedger.Services;
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
    [RequestSizeLimit(FileStorageService.MaxAvatarBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileStorageService.MaxAvatarBytes)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Пустой файл" });
        if (file.Length > FileStorageService.MaxAvatarBytes)
            return BadRequest(new { message = "Аватар не больше 2 МБ" });

        try
        {
            await using var stream = file.OpenReadStream();
            var key = await fileStorage.UploadAvatarAsync(
                User.GetUserId(), stream, file.ContentType, file.Length, ct);
            var user = await userService.SetAvatarKeyAsync(User.GetUserId(), key, ct);
            var url = await fileStorage.GetAvatarUrlAsync(user.AvatarKey, ct);
            return Ok(new { avatarUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

[ApiController]
[Authorize]
public class FilesController(IFileStorageService fileStorage) : ControllerBase
{
    [HttpGet("/api/files/{*objectKey}")]
    public async Task<IActionResult> Get(string objectKey, CancellationToken ct)
    {
        var result = await fileStorage.OpenReadAsync(objectKey, ct);
        if (result is null) return NotFound();
        return File(result.Value.Stream, result.Value.ContentType);
    }
}
