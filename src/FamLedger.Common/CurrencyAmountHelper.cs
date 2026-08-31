using System.Text.Json;
using FamLedger.Domain.Models;

namespace FamLedger.Common;

public static class CurrencyAmountHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Dictionary<string, decimal> ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOptions)
                ?? new Dictionary<string, decimal>();
            return parsed.ToDictionary(
                k => k.Key.ToUpperInvariant(),
                v => v.Value,
                StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static string ToJson(IReadOnlyDictionary<string, decimal> amounts)
    {
        var normalized = amounts
            .Where(x => x.Value != 0)
            .ToDictionary(x => x.Key.ToUpperInvariant(), x => x.Value, StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static IReadOnlyList<CurrencyAmount> ToAmounts(IReadOnlyDictionary<string, decimal> amounts) =>
        amounts
            .Where(x => x.Value != 0)
            .OrderBy(x => x.Key)
            .Select(x => new CurrencyAmount(x.Key, x.Value))
            .ToList();

    public static Dictionary<string, decimal> MergeAmounts(params IEnumerable<KeyValuePair<string, decimal>>[] sources)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var (currency, amount) in source)
            {
                if (amount == 0) continue;
                var code = currency.ToUpperInvariant();
                result[code] = result.GetValueOrDefault(code) + amount;
            }
        }

        return result;
    }

    public static IReadOnlyList<CurrencyAmount> SubtractTotals(
        IReadOnlyDictionary<string, decimal> left,
        IReadOnlyDictionary<string, decimal> right)
    {
        var currencies = left.Keys.Concat(right.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        var result = new List<CurrencyAmount>();
        foreach (var currency in currencies.OrderBy(x => x))
        {
            var amount = left.GetValueOrDefault(currency) - right.GetValueOrDefault(currency);
            if (amount != 0)
                result.Add(new CurrencyAmount(currency, amount));
        }

        return result;
    }
}
