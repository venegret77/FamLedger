using System.Globalization;

namespace FamLedger.Common;

public static class MoneyFormatter
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static string Format(decimal amount, string currency) =>
        currency.ToUpperInvariant() switch
        {
            "EUR" => $"{FormatDecimal(amount)} €",
            "USD" => $"${FormatDecimal(amount)}",
            _ => $"{amount.ToString("N0", Ru)} RSD"
        };

    public static string FormatShort(decimal amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal amount) =>
        amount == decimal.Truncate(amount)
            ? amount.ToString("0", CultureInfo.InvariantCulture)
            : amount.ToString("0.##", CultureInfo.InvariantCulture);
}
