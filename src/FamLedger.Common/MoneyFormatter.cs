using System.Globalization;

namespace FamLedger.Common;

public static class MoneyFormatter
{
    public static string Format(decimal amount, string currency) =>
        currency.ToUpperInvariant() switch
        {
            "EUR" => $"{amount:N0} €",
            "USD" => $"${amount:N0}",
            _ => $"{amount:N0} RSD"
        };

    public static string FormatShort(decimal amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture);
}
