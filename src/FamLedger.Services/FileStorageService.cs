using FamLedger.Interfaces.Services;
using FamLedger.Interfaces.Settings;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace FamLedger.Services;

public class FileStorageService(IAppSettings settings) : IFileStorageService
{
    public const long MaxAvatarBytes = 2 * 1024 * 1024; // 2 MB

    private IMinioClient CreateClient() =>
        new MinioClient()
            .WithEndpoint(settings.MinioEndpoint)
            .WithCredentials(settings.MinioAccessKey, settings.MinioSecretKey)
            .WithSSL(false)
            .Build();

    public async Task<string> UploadAvatarAsync(Guid userId, Stream stream, long size, CancellationToken ct = default)
    {
        if (size <= 0)
            throw new InvalidOperationException("Empty file");
        if (size > MaxAvatarBytes)
            throw new InvalidOperationException($"Avatar must be at most {MaxAvatarBytes / (1024 * 1024)} MB");
        var bytes = new byte[(int)size];
        await stream.ReadExactlyAsync(bytes, ct);
        var format = DetectImageFormat(bytes);
        if (format is null)
            throw new InvalidOperationException("Only JPEG, PNG, WebP or GIF images are allowed");

        using var client = CreateClient();
        using var content = new MemoryStream(bytes, writable: false);
        var bucket = settings.MinioBucket;
        var key = $"avatars/{userId}/{Guid.NewGuid():N}.{format.Value.Extension}";

        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);

        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(format.Value.ContentType), ct);

        return key;
    }

    public Task<string?> GetAvatarUrlAsync(string? avatarKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(avatarKey))
            return Task.FromResult<string?>(null);
        // Served by API so the browser never needs the internal MinIO host.
        return Task.FromResult<string?>($"/api/files/{avatarKey}");
    }

    public async Task<(Stream Stream, string ContentType)?> OpenReadAsync(string objectKey, CancellationToken ct = default)
    {
        if (!IsAvatarKey(objectKey)) return null;
        using var client = CreateClient();
        var bucket = settings.MinioBucket;

        try
        {
            var metadata = await client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(bucket).WithObject(objectKey), ct);
            if (metadata.Size is <= 0 or > MaxAvatarBytes) return null;

            var bytes = new byte[(int)metadata.Size];
            await client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithCallbackStream(source => source.ReadExactly(bytes)), ct);
            var format = DetectImageFormat(bytes);
            return format is null ? null : (new MemoryStream(bytes, writable: false), format.Value.ContentType);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task DeleteAvatarAsync(string objectKey, CancellationToken ct = default)
    {
        if (!IsAvatarKey(objectKey)) return;
        using var client = CreateClient();
        await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(settings.MinioBucket).WithObject(objectKey), ct);
    }

    private static bool IsAvatarKey(string key)
    {
        var parts = key.Split('/');
        if (parts.Length != 3 || parts[0] != "avatars" || !Guid.TryParseExact(parts[1], "D", out _))
            return false;

        var file = parts[2].Split('.');
        return file.Length == 2 && Guid.TryParseExact(file[0], "N", out _)
            && file[1] is "jpg" or "png" or "webp" or "gif";
    }

    private static (string Extension, string ContentType)? DetectImageFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
            return ("jpg", "image/jpeg");
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return ("png", "image/png");
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8))
            return ("gif", "image/gif");
        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return ("webp", "image/webp");
        return null;
    }
}
