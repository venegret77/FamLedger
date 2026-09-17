using System.Net.Http.Json;
using System.Text.Json;
using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.ValueObjects;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class ExchangeRateService(
    AppDbContext db,
    IRedisService redis,
    IHttpClientFactory httpClientFactory) : IExchangeRateService
{
    private const string NbgUsdRatesUrl =
        "https://nbg.gov.ge/gw/api/ct/monetarypolicy/currencies/en/json?currencies=USD";

    public async Task<decimal> GetRateAsync(string currency, DateOnly date, Guid contextId, Guid? periodId, CancellationToken ct = default)
    {
        if (currency.Equals(CurrencyCode.Rsd, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var code = currency.ToUpperInvariant();

        var overrideRate = await db.RateOverrides
            .Where(r =>
                r.ContextId == contextId &&
                r.Currency == code &&
                (r.PeriodId == null || r.PeriodId == periodId) &&
                r.RateToRsd >= FxConversion.MinRateToRsd &&
                r.RateToRsd <= FxConversion.MaxRateToRsd)
            .Select(r => (decimal?)r.RateToRsd)
            .FirstOrDefaultAsync(ct);
        if (overrideRate is not null)
            return overrideRate.Value;

        var rate = await FindStoredRateAsync(code, date, ct);
        if (rate is not null)
            return rate.Value;

        // Нет курса в БД — тянем актуальный с NBS через Kurs API, не fallback.
        await FetchAndStoreRatesAsync(ct);

        rate = await FindStoredRateAsync(code, date, ct);
        return rate ?? GetFallbackRate(code);
    }

    public async Task<decimal> ConvertToBaseAsync(decimal amount, string currency, DateOnly date, Guid contextId, Guid? periodId, CancellationToken ct = default)
    {
        var rate = await GetRateAsync(currency, date, contextId, periodId, ct);
        if (FxConversion.TryMultiplyToBase(amount, rate, out var baseAmount))
            return baseAmount;

        var fallback = GetFallbackRate(currency.ToUpperInvariant());
        if (FxConversion.TryMultiplyToBase(amount, fallback, out baseAmount))
            return baseAmount;

        return amount;
    }

    public async Task FetchAndStoreRatesAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var client = httpClientFactory.CreateClient("KursApi");
        decimal? usdToRsd = null;

        foreach (var currency in new[] { CurrencyCode.Eur, CurrencyCode.Usd })
        {
            try
            {
                // Correct Kurs API: /api/v1/currencies/{code}/rates/today
                var response = await client.GetAsync($"api/v1/currencies/{currency.ToLowerInvariant()}/rates/today", ct);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (!TryReadMiddleRate(json, out var middleRate)) continue;
                if (!FxConversion.IsPlausibleRateToRsd(middleRate)) continue;

                await UpsertRateAsync(today, currency, middleRate, ct);

                if (currency == CurrencyCode.Usd && FxConversion.IsPlausibleUsdToRsd(middleRate))
                    usdToRsd = middleRate;

                await redis.SetAsync(CacheKeys.FxRates(today), middleRate.ToString(System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromHours(6));
            }
            catch
            {
                // keep last known rates
            }
        }

        if (usdToRsd is null)
        {
            usdToRsd = await db.ExchangeRates
                .Where(r =>
                    r.Currency == CurrencyCode.Usd &&
                    r.RateToRsd >= FxConversion.MinRateToRsd &&
                    r.RateToRsd <= FxConversion.MaxRateToRsd)
                .OrderByDescending(r => r.Date)
                .Select(r => (decimal?)r.RateToRsd)
                .FirstOrDefaultAsync(ct);

            if (usdToRsd is not null && !FxConversion.IsPlausibleUsdToRsd(usdToRsd.Value))
                usdToRsd = null;
        }

        await FetchAndStoreGelRateAsync(today, usdToRsd, ct);

        await db.SaveChangesAsync(ct);
        await RecalculateOpenPeriodAmountsAsync(ct);
    }

    private async Task FetchAndStoreGelRateAsync(DateOnly today, decimal? usdToRsd, CancellationToken ct)
    {
        if (usdToRsd is null || !FxConversion.IsPlausibleUsdToRsd(usdToRsd.Value))
            return;

        try
        {
            var client = httpClientFactory.CreateClient("NbgApi");
            var response = await client.GetAsync(NbgUsdRatesUrl, ct);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!TryReadNbgUsdToGel(json, out var usdToGel) || !FxConversion.IsPlausibleUsdToGel(usdToGel))
                return;

            // 1 GEL = (USD→RSD) / (USD→GEL)
            var gelToRsd = decimal.Round(usdToRsd.Value / usdToGel, 6, MidpointRounding.AwayFromZero);
            if (!FxConversion.IsPlausibleRateToRsd(gelToRsd))
                return;

            await UpsertRateAsync(today, CurrencyCode.Gel, gelToRsd, ct);
        }
        catch
        {
            // keep last known rates
        }
    }

    private async Task UpsertRateAsync(DateOnly today, string currency, decimal rateToRsd, CancellationToken ct)
    {
        if (!FxConversion.IsPlausibleRateToRsd(rateToRsd))
            return;

        // Select Id only — never materialize an oversized RateToRsd into System.Decimal.
        var existingId = await db.ExchangeRates
            .Where(r => r.Date == today && r.Currency == currency)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (existingId is null)
        {
            db.ExchangeRates.Add(new ExchangeRate
            {
                Date = today,
                Currency = currency,
                RateToRsd = rateToRsd
            });
            return;
        }

        await db.ExchangeRates
            .Where(r => r.Id == existingId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.RateToRsd, rateToRsd)
                .SetProperty(r => r.FetchedAt, DateTime.UtcNow), ct);
    }

    private async Task RecalculateOpenPeriodAmountsAsync(CancellationToken ct)
    {
        var openItems = await db.PeriodRecurringItems
            .Include(i => i.RecurringExpense)
            .Include(i => i.Period)
            .Where(i => !i.Period.IsClosed)
            .ToListAsync(ct);

        foreach (var item in openItems)
        {
            var expense = item.RecurringExpense;
            if (expense.DefinitionCurrency.Equals(CurrencyCode.Rsd, StringComparison.OrdinalIgnoreCase))
            {
                item.PlannedBaseAmount = expense.DefinitionAmount;
                continue;
            }

            var rate = await FindLatestRateAsync(expense.DefinitionCurrency, ct);
            if (rate is null) continue;
            if (FxConversion.TryMultiplyToBase(expense.DefinitionAmount, rate.Value, out var baseAmount))
                item.PlannedBaseAmount = baseAmount;
        }

        var openOneOff = await db.OneOffExpenses
            .Include(o => o.Period)
            .Where(o => !o.Period.IsClosed)
            .ToListAsync(ct);

        foreach (var expense in openOneOff)
        {
            if (expense.Currency.Equals(CurrencyCode.Rsd, StringComparison.OrdinalIgnoreCase))
            {
                expense.BaseAmount = expense.Amount;
                continue;
            }

            var rate = await FindLatestRateAsync(expense.Currency, ct);
            if (rate is null) continue;
            if (FxConversion.TryMultiplyToBase(expense.Amount, rate.Value, out var baseAmount))
                expense.BaseAmount = baseAmount;
        }

        await db.SaveChangesAsync(ct);
    }

    private Task<decimal?> FindStoredRateAsync(string code, DateOnly date, CancellationToken ct) =>
        db.ExchangeRates
            .Where(r =>
                r.Currency == code &&
                r.Date <= date &&
                r.RateToRsd >= FxConversion.MinRateToRsd &&
                r.RateToRsd <= FxConversion.MaxRateToRsd)
            .OrderByDescending(r => r.Date)
            .Select(r => (decimal?)r.RateToRsd)
            .FirstOrDefaultAsync(ct);

    private Task<decimal?> FindLatestRateAsync(string currency, CancellationToken ct) =>
        db.ExchangeRates
            .Where(r =>
                r.Currency == currency.ToUpperInvariant() &&
                r.RateToRsd >= FxConversion.MinRateToRsd &&
                r.RateToRsd <= FxConversion.MaxRateToRsd)
            .OrderByDescending(r => r.Date)
            .Select(r => (decimal?)r.RateToRsd)
            .FirstOrDefaultAsync(ct);

    private static bool TryReadMiddleRate(JsonElement json, out decimal rate)
    {
        rate = 0m;
        if (json.TryGetProperty("exchange_middle", out var middle) && middle.TryGetDecimal(out rate))
            return true;
        if (json.TryGetProperty("rate", out var legacy) && legacy.TryGetDecimal(out rate))
            return true;
        return false;
    }

    /// <summary>NBG returns USD priced in GEL (how many lari for <c>quantity</c> USD).</summary>
    private static bool TryReadNbgUsdToGel(JsonElement json, out decimal usdToGel)
    {
        usdToGel = 0m;
        if (json.ValueKind != JsonValueKind.Array || json.GetArrayLength() == 0)
            return false;

        var day = json[0];
        if (!day.TryGetProperty("currencies", out var currencies) || currencies.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in currencies.EnumerateArray())
        {
            if (!item.TryGetProperty("code", out var codeEl)) continue;
            if (!string.Equals(codeEl.GetString(), CurrencyCode.Usd, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!item.TryGetProperty("rate", out var rateEl) || !rateEl.TryGetDecimal(out var rate))
                return false;

            var quantity = 1m;
            if (item.TryGetProperty("quantity", out var qtyEl) && qtyEl.TryGetDecimal(out var qty) && qty > 0m)
                quantity = qty;

            usdToGel = rate / quantity;
            return usdToGel > 0m;
        }

        return false;
    }

    private static decimal GetFallbackRate(string currency) =>
        currency.ToUpperInvariant() switch
        {
            // Last-resort only if Kurs API unreachable; approximate NBS middle.
            CurrencyCode.Eur => 117.36m,
            CurrencyCode.Usd => 100.52m,
            // ~USD/RSD ÷ ~USD/GEL (NBG)
            CurrencyCode.Gel => 38.5m,
            _ => 1m
        };
}
