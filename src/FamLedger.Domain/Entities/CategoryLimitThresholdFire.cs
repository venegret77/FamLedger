namespace FamLedger.Domain.Entities;

public class CategoryLimitThresholdFire
{
    public Guid LimitId { get; set; }
    public int ThresholdPercent { get; set; }
    public DateOnly LastFiredDateUtc { get; set; }

    public CategorySpendingLimit Limit { get; set; } = null!;
}
