namespace FamLedger.Domain.Entities;

public class SavingsEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid PeriodId { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public string Currency { get; set; } = ValueObjects.CurrencyCode.Rsd;

    public BudgetContext Context { get; set; } = null!;
    public BudgetPeriod Period { get; set; } = null!;
}
