using FamLedger.Api.Extensions;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using FamLedger.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(
    IUserService userService,
    IFileStorageService fileStorage,
    ILogger<UsersController> logger) : ControllerBase
{
    public record UpdateProfileRequest(string? DisplayName);

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await userService.UpdateProfileAsync(User.GetUserId(), request.DisplayName, ct);
        return Ok(new { user.DisplayName });
    }

    [HttpPost("me/avatar")]
    [RequestSizeLimit(FileStorageService.MaxAvatarBytes + 16 * 1024)]
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
            var userId = User.GetUserId();
            var key = await fileStorage.UploadAvatarAsync(userId, stream, file.Length, ct);
            var oldKey = await userService.ReplaceAvatarKeyAsync(userId, key, ct);
            if (oldKey is not null && oldKey.StartsWith($"avatars/{userId}/", StringComparison.Ordinal))
            {
                try
                {
                    await fileStorage.DeleteAvatarAsync(oldKey, ct);
                }
                catch (Minio.Exceptions.MinioException ex)
                {
                    logger.LogWarning(ex, "Could not remove a replaced avatar");
                }
            }
            var url = await fileStorage.GetAvatarUrlAsync(key, ct);
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
public class FilesController(IFileStorageService fileStorage, AppDbContext db) : ControllerBase
{
    [HttpGet("/api/files/{*objectKey}")]
    public async Task<IActionResult> Get(string objectKey, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var canRead = await db.Users.AnyAsync(owner => owner.AvatarKey == objectKey
            && (owner.Id == userId || owner.Memberships.Any(membership => !membership.Context.IsPersonal
                && membership.Context.Members.Any(member => member.UserId == userId))), ct);
        if (!canRead) return NotFound();

        var result = await fileStorage.OpenReadAsync(objectKey, ct);
        if (result is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Value.Stream, result.Value.ContentType);
    }
}
