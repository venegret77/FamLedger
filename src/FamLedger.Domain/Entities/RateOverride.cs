namespace FamLedger.Domain.Entities;

public class RateOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid? PeriodId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal RateToRsd { get; set; }

    public BudgetContext Context { get; set; } = null!;
    public BudgetPeriod? Period { get; set; }
}
