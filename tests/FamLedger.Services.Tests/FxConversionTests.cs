using FamLedger.Common;

namespace FamLedger.Services.Tests;

public class FxConversionTests
{
    [Fact]
    public void TryMultiplyToBase_Should_ConvertGelWithPlausibleRate()
    {
        var ok = FxConversion.TryMultiplyToBase(100m, 38.5m, out var baseAmount);

        Assert.True(ok);
        Assert.Equal(3850m, baseAmount);
    }

    [Fact]
    public void TryMultiplyToBase_Should_RejectAbsurdRate()
    {
        var ok = FxConversion.TryMultiplyToBase(100m, 1_000_000_000m, out var baseAmount);

        Assert.False(ok);
        Assert.Equal(0m, baseAmount);
    }

    [Fact]
    public void IsPlausibleUsdToGel_Should_AcceptNbgRange()
    {
        Assert.True(FxConversion.IsPlausibleUsdToGel(2.6076m));
        Assert.False(FxConversion.IsPlausibleUsdToGel(0.0000001m));
        Assert.False(FxConversion.IsPlausibleUsdToGel(250m));
    }

    [Fact]
    public void GelCrossRate_Should_StayPlausible()
    {
        var usdToRsd = 102.3637m;
        var usdToGel = 2.6076m;
        var gelToRsd = decimal.Round(usdToRsd / usdToGel, 6, MidpointRounding.AwayFromZero);

        Assert.True(FxConversion.IsPlausibleRateToRsd(gelToRsd));
        Assert.InRange(gelToRsd, 30m, 50m);
    }
}
