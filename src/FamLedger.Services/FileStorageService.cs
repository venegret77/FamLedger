using FamLedger.Interfaces.Services;
using FamLedger.Interfaces.Settings;
using Minio;
using Minio.DataModel.Args;

namespace FamLedger.Services;

public class FileStorageService(IAppSettings settings) : IFileStorageService
{
    public const long MaxAvatarBytes = 2 * 1024 * 1024; // 2 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private IMinioClient CreateClient() =>
        new MinioClient()
            .WithEndpoint(settings.MinioEndpoint)
            .WithCredentials(settings.MinioAccessKey, settings.MinioSecretKey)
            .WithSSL(false)
            .Build();

    public async Task<string> UploadAvatarAsync(Guid userId, Stream stream, string contentType, long size, CancellationToken ct = default)
    {
        if (size <= 0)
            throw new InvalidOperationException("Empty file");
        if (size > MaxAvatarBytes)
            throw new InvalidOperationException($"Avatar must be at most {MaxAvatarBytes / (1024 * 1024)} MB");
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidOperationException("Only JPEG, PNG, WebP or GIF images are allowed");

        var client = CreateClient();
        var bucket = settings.MinioBucket;
        var ext = contentType.ToLowerInvariant() switch
        {
            "image/png" => "png",
            "image/webp" => "webp",
            "image/gif" => "gif",
            _ => "jpg"
        };
        var key = $"avatars/{userId}/{Guid.NewGuid():N}.{ext}";

        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);

        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType), ct);

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
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Contains("..", StringComparison.Ordinal))
            return null;

        var client = CreateClient();
        var bucket = settings.MinioBucket;
        var memory = new MemoryStream();
        string contentType = "application/octet-stream";

        try
        {
            await client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithCallbackStream(s => s.CopyTo(memory)), ct);
        }
        catch (Exception)
        {
            await memory.DisposeAsync();
            return null;
        }

        memory.Position = 0;
        contentType = GuessContentType(objectKey);
        return (memory, contentType);
    }

    private static string GuessContentType(string key) =>
        Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
}
