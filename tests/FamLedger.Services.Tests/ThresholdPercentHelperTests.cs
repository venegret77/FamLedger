using FamLedger.Common;

namespace FamLedger.Services.Tests;

public class ThresholdPercentHelperTests
{
    [Fact]
    public void Normalize_Should_SortUniqueAndFallback()
    {
        Assert.Equal([25, 50, 75, 95], ThresholdPercentHelper.DefaultCategoryLimit);
        Assert.Equal([50, 80], ThresholdPercentHelper.Normalize([80, 50, 50, 0, 101], [80]));
        Assert.Equal([80], ThresholdPercentHelper.Normalize(null, ThresholdPercentHelper.DefaultBudgetAlert));
    }

    [Fact]
    public void GetNewlyCrossed_Should_SkipAlreadyFired()
    {
        var newly = ThresholdPercentHelper.GetNewlyCrossed(
            [50, 80, 90],
            percentUsed: 85,
            alreadyFiredToday: new HashSet<int> { 50 });

        Assert.Equal([80], newly);
    }
}
