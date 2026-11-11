using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using FamLedger.Api.Extensions;
using FamLedger.Interfaces.Settings;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

const string obsoleteJwtSecret = "change-me-in-production-min-32-chars!!";
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["App:JwtSecret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret == obsoleteJwtSecret || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Set JWT_SECRET to a strong, randomly generated value of at least 32 bytes.");

    jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}

builder.Services.AddFamLedgerSettings(builder.Configuration);
builder.Services.PostConfigure<AppSettings>(options => options.JwtSecret = jwtSecret);

builder.Services.AddFamLedgerDb(builder.Configuration);
builder.Services.AddFamLedgerRedis(builder.Configuration);
builder.Services.AddFamLedgerServices(includeTelegramBot: false);
builder.Services.AddFamLedgerAuth();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["WEB_ORIGIN"] ?? "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    var trustedProxy = builder.Configuration["TRUSTED_PROXY_IP"];
    if (!string.IsNullOrWhiteSpace(trustedProxy))
        options.KnownProxies.Add(IPAddress.Parse(trustedProxy));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("bot-login", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6
        });
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "bot-login"
            ? RateLimitPartition.GetSlidingWindowLimiter("bot-login", _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6
            })
            : RateLimitPartition.GetNoLimiter("other"));
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["CONNECTION_STRING"] ?? "")
    .AddRedis(builder.Configuration["Redis:ConnectionString"] ?? builder.Configuration["REDIS_CONNECTION_STRING"] ?? "localhost:6382");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    try
    {
        var rates = scope.ServiceProvider.GetRequiredService<FamLedger.Interfaces.Services.IExchangeRateService>();
        await rates.FetchAndStoreRatesAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to fetch exchange rates on startup");
    }
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseForwardedHeaders();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<FamLedger.Api.Middleware.UserActivityMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/api/health");

app.Run();
