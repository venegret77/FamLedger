namespace FamLedger.Common;

/// <summary>Guards FX rates and money conversion so PostgreSQL <c>numeric</c> values stay within System.Decimal.</summary>
public static class FxConversion
{
    /// <summary>Max plausible units-of-currency → RSD (covers EUR/USD/GEL with headroom).</summary>
    public const decimal MaxRateToRsd = 10_000m;

    public const decimal MinRateToRsd = 0.0001m;

    /// <summary>Reject money totals beyond this when aggregating (still far above real budgets).</summary>
    public const decimal MaxMoneyAmount = 1_000_000_000_000m; // 1e12

    public static bool IsPlausibleRateToRsd(decimal rate) =>
        rate >= MinRateToRsd && rate <= MaxRateToRsd;

    public static bool IsPlausibleUsdToGel(decimal usdToGel) =>
        usdToGel >= 0.5m && usdToGel <= 20m;

    public static bool IsPlausibleUsdToRsd(decimal usdToRsd) =>
        usdToRsd >= 50m && usdToRsd <= 300m;

    public static bool TryMultiplyToBase(decimal amount, decimal rateToRsd, out decimal baseAmount)
    {
        baseAmount = 0m;
        if (!IsPlausibleRateToRsd(rateToRsd))
            return false;
        if (amount == 0m)
            return true;

        try
        {
            var product = decimal.Round(amount * rateToRsd, 4, MidpointRounding.AwayFromZero);
            if (Math.Abs(product) > MaxMoneyAmount)
                return false;
            baseAmount = product;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
