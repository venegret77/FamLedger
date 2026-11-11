using FamLedger.Interfaces.Settings;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace FamLedger.Services.Tests;

public sealed class LoginAndWebhookTests
{
    [RedisFact]
    public async Task RedisLoginCodeCanOnlyBeConsumedOnceConcurrently()
    {
        var options = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("FAMLEDGER_TEST_REDIS")!);
        Assert.All(options.EndPoints, endpoint => Assert.True(endpoint switch
        {
            System.Net.DnsEndPoint { Host: "localhost" or "127.0.0.1" } => true,
            System.Net.IPEndPoint ip => System.Net.IPAddress.IsLoopback(ip.Address),
            _ => false
        }));
        using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var login = new LoginTokenService(new RedisService(connection));

        var code = await login.CreateAsync(42);
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Null(await login.ConsumeAsync("prefix-" + code));
        var values = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => login.ConsumeAsync(code)));
        Assert.Single(values, value => value == 42);
        Assert.Equal(7, values.Count(value => value is null));
    }

    [Fact]
    public void WebhookRequiresAnExactApprovedHttpsDestination()
    {
        var settings = Options.Create(new AppSettings { AllowedWebhookUrls = ["https://example.com/notifications"] });
        var destinations = new WebhookDestinations(settings);
        Assert.NotNull(destinations.Resolve("https://example.com/notifications"));
        Assert.Null(destinations.Resolve("https://example.com/another-path"));
        Assert.Null(destinations.Resolve("http://example.com/notifications"));
        Assert.Null(destinations.Resolve("https://example.com/notifications?new=target"));
        Assert.Null(destinations.Resolve("https://127.0.0.1/notifications"));
    }
}

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FAMLEDGER_TEST_REDIS")))
            Skip = "Set FAMLEDGER_TEST_REDIS to a dedicated local Redis server.";
    }
}
