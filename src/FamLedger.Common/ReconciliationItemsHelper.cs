using System.Text.Json;
using FamLedger.Domain.Models;

namespace FamLedger.Common;

public static class ReconciliationItemsHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static List<ReconciliationManualEntry> ParseItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<ReconciliationManualEntry>>(json, JsonOptions) ?? [];
            return parsed
                .Where(x => x.Amount != 0 && !string.IsNullOrWhiteSpace(x.Name))
                .Select(Normalize)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string ToJson(IReadOnlyList<ReconciliationManualEntry> items)
    {
        var normalized = items
            .Where(x => x.Amount != 0 && !string.IsNullOrWhiteSpace(x.Name))
            .Select(Normalize)
            .ToList();
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static Dictionary<string, decimal> ToCurrencyTotals(IEnumerable<ReconciliationManualEntry> items)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item.Amount == 0) continue;
            var code = item.Currency.ToUpperInvariant();
            result[code] = result.GetValueOrDefault(code) + item.Amount;
        }

        return result;
    }

    private static ReconciliationManualEntry Normalize(ReconciliationManualEntry item) =>
        new(
            item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
            item.Name.Trim(),
            item.Amount,
            item.Currency.ToUpperInvariant());
}
