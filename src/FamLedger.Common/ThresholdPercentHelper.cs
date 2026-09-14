namespace FamLedger.Common;

public static class ThresholdPercentHelper
{
    public const int MaxCount = 10;
    public static readonly int[] DefaultBudgetAlert = [80];
    public static readonly int[] DefaultCategoryLimit = [25, 50, 75, 95];

    public static int[] Normalize(IEnumerable<int>? percents, int[] fallback)
    {
        var list = (percents ?? [])
            .Where(p => p is >= 1 and <= 100)
            .Distinct()
            .OrderBy(p => p)
            .Take(MaxCount)
            .ToArray();
        return list.Length > 0 ? list : fallback;
    }

    public static IReadOnlyList<int> GetNewlyCrossed(
        IEnumerable<int> configured,
        int percentUsed,
        IReadOnlySet<int> alreadyFiredToday)
    {
        return configured
            .Where(t => percentUsed >= t && !alreadyFiredToday.Contains(t))
            .OrderBy(t => t)
            .ToList();
    }
}
