using FamLedger.Interfaces.Settings;
using Microsoft.Extensions.Options;

namespace FamLedger.Services;

public class WebhookDestinations(IOptions<AppSettings> settings)
{
    public Uri? Resolve(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.HostNameType != UriHostNameType.Dns
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
            return null;

        return settings.Value.AllowedWebhookUrls.Contains(uri.AbsoluteUri, StringComparer.Ordinal) ? uri : null;
    }
}
