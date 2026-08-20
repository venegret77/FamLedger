namespace FamLedger.Domain.Entities;

public class OneOffExpense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid PeriodId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public decimal BaseAmount { get; set; }
    public bool IsPaid { get; set; }

    public BudgetContext Context { get; set; } = null!;
    public BudgetPeriod Period { get; set; } = null!;
}
