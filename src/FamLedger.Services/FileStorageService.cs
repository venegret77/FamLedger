using FamLedger.Interfaces.Services;
using FamLedger.Interfaces.Settings;
using Minio;
using Minio.DataModel.Args;

namespace FamLedger.Services;

public class FileStorageService(IAppSettings settings) : IFileStorageService
{
    private MinioClient CreateClient() =>
        (MinioClient)new MinioClient()
            .WithEndpoint(settings.MinioEndpoint)
            .WithCredentials(settings.MinioAccessKey, settings.MinioSecretKey)
            .WithSSL(false)
            .Build();

    public async Task<string> UploadAvatarAsync(Guid userId, Stream stream, string contentType, CancellationToken ct = default)
    {
        var client = CreateClient();
        var bucket = settings.MinioBucket;
        var key = $"avatars/{userId}/{Guid.NewGuid()}.jpg";

        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);

        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType), ct);

        return key;
    }

    public Task<string?> GetAvatarUrlAsync(string? avatarKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(avatarKey)) return Task.FromResult<string?>(null);
        return Task.FromResult<string?>($"http://{settings.MinioEndpoint}/{settings.MinioBucket}/{avatarKey}");
    }
}
