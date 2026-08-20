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
            ["rsd"] = CurrencyCode.Rsd,
            ["din"] = CurrencyCode.Rsd,
            ["dinar"] = CurrencyCode.Rsd,
            ["dinars"] = CurrencyCode.Rsd,
            ["дин"] = CurrencyCode.Rsd,
            ["динар"] = CurrencyCode.Rsd,
            ["динара"] = CurrencyCode.Rsd,
            ["динаров"] = CurrencyCode.Rsd,
            ["d"] = CurrencyCode.Rsd,

            ["eur"] = CurrencyCode.Eur,
            ["euro"] = CurrencyCode.Eur,
            ["euros"] = CurrencyCode.Eur,
            ["e"] = CurrencyCode.Eur,
            ["€"] = CurrencyCode.Eur,
            ["евро"] = CurrencyCode.Eur,

            ["usd"] = CurrencyCode.Usd,
            ["dollar"] = CurrencyCode.Usd,
            ["dollars"] = CurrencyCode.Usd,
            ["$"] = CurrencyCode.Usd,
            ["долл"] = CurrencyCode.Usd,
            ["доллар"] = CurrencyCode.Usd,
            ["доллара"] = CurrencyCode.Usd,
            ["долларов"] = CurrencyCode.Usd,
            ["бакс"] = CurrencyCode.Usd,
            ["бакса"] = CurrencyCode.Usd,
            ["баксов"] = CurrencyCode.Usd,
        };

    public static bool TryParse(string? text, out ParsedMoneyInput result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var raw = text.Trim().Replace('\u00A0', ' ');

        // $10 / €12,5
        var symbolPrefix = SymbolPrefixRegex().Match(raw);
        if (symbolPrefix.Success)
            return Finish(symbolPrefix.Groups["amount"].Value, symbolPrefix.Groups["symbol"].Value, out result);

        // 10$ / 12,5€
        var symbolSuffix = SymbolSuffixRegex().Match(raw);
        if (symbolSuffix.Success)
            return Finish(symbolSuffix.Groups["amount"].Value, symbolSuffix.Groups["symbol"].Value, out result);

        // 10usd / 10eur / 12,5RSD (код сразу после числа)
        var glued = GluedCurrencyRegex().Match(raw);
        if (glued.Success && TryResolveCurrency(glued.Groups["code"].Value, out var gluedCurrency))
            return Finish(glued.Groups["amount"].Value, gluedCurrency, out result);

        // usd 10 | 10 usd | euro 10 | 10 евро
        var spaced = SpacedCurrencyRegex().Match(raw);
        if (spaced.Success && TryResolveCurrency(spaced.Groups["code"].Value, out var spacedCurrency))
            return Finish(spaced.Groups["amount"].Value, spacedCurrency, out result);

        // просто число → RSD
        var plain = PlainAmountRegex().Match(raw);
        if (plain.Success)
            return Finish(plain.Groups["amount"].Value, CurrencyCode.Rsd, out result);

        return false;
    }

    private static bool Finish(string amountText, string currencyOrToken, out ParsedMoneyInput result)
    {
        result = default;
        amountText = amountText.Replace(" ", "").Replace(',', '.');
        if (!decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return false;

        if (!TryResolveCurrency(currencyOrToken, out var currency))
            currency = CurrencyCode.Rsd;

        result = new ParsedMoneyInput(amount, currency, null);
        return true;
    }

    private static bool TryResolveCurrency(string token, out string currency)
    {
        currency = CurrencyCode.Rsd;
        token = token.Trim().TrimEnd(',', '.', '!');
        if (token.Length == 0) return false;
        if (token is "€" || token.StartsWith('€'))
        {
            currency = CurrencyCode.Eur;
            return true;
        }
        if (token is "$" || token.StartsWith('$'))
        {
            currency = CurrencyCode.Usd;
            return true;
        }

        return CurrencyAliases.TryGetValue(token, out currency!);
    }

    [GeneratedRegex(@"^(?<symbol>[€$])\s*(?<amount>[\d\s]+(?:[.,]\d+)?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SymbolPrefixRegex();

    [GeneratedRegex(@"^(?<amount>[\d\s]+(?:[.,]\d+)?)\s*(?<symbol>[€$])\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SymbolSuffixRegex();

    [GeneratedRegex(
        @"^(?<amount>[\d\s]+(?:[.,]\d+)?)(?<code>usd|eur|rsd|euro|euros|dollar|dollars|din|dinar|евро|долл(?:ар(?:а|ов)?)?|дин(?:ар(?:а|ов)?)?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex GluedCurrencyRegex();

    [GeneratedRegex(
        @"^(?:(?<code>[A-Za-zА-Яа-яЁё€$]+)\s+(?<amount>[\d\s]+(?:[.,]\d+)?)|(?<amount>[\d\s]+(?:[.,]\d+)?)\s+(?<code>[A-Za-zА-Яа-яЁё€$]+))\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SpacedCurrencyRegex();

    [GeneratedRegex(@"^(?<amount>[\d\s]+(?:[.,]\d+)?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainAmountRegex();
}
