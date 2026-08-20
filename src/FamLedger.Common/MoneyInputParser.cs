using System.Globalization;
using System.Text.RegularExpressions;
using FamLedger.Domain.Models;
using FamLedger.Domain.ValueObjects;

namespace FamLedger.Common;

public static partial class MoneyInputParser
{
    private static readonly Dictionary<string, string> CurrencyAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rsd"] = CurrencyCode.Rsd, ["din"] = CurrencyCode.Rsd, ["дин"] = CurrencyCode.Rsd,
            ["d"] = CurrencyCode.Rsd, ["eur"] = CurrencyCode.Eur, ["e"] = CurrencyCode.Eur,
            ["€"] = CurrencyCode.Eur, ["евро"] = CurrencyCode.Eur,
            ["usd"] = CurrencyCode.Usd, ["$"] = CurrencyCode.Usd, ["долл"] = CurrencyCode.Usd
        };

    public static bool TryParse(string? text, out ParsedMoneyInput result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = MoneyInputRegex().Match(text.Trim());
        if (!match.Success) return false;

        var amountText = match.Groups["amount"].Value.Replace(" ", "").Replace(',', '.');
        if (!decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return false;

        var tail = match.Groups["tail"].Value.Trim();
        var currency = CurrencyCode.Rsd;

        if (!string.IsNullOrEmpty(tail))
        {
            if (tail.StartsWith('€')) currency = CurrencyCode.Eur;
            else if (tail.StartsWith('$')) currency = CurrencyCode.Usd;
            else
            {
                var token = tail.Split(' ')[0].Trim().TrimEnd(',', '.');
                if (CurrencyAliases.TryGetValue(token, out var resolved))
                    currency = resolved;
            }
        }

        result = new ParsedMoneyInput(amount, currency, null);
        return true;
    }

    [GeneratedRegex(@"^(?<amount>[\d\s]+(?:[.,]\d+)?)\s*(?<tail>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex MoneyInputRegex();
}
