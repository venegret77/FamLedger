namespace FamLedger.Domain.Entities;

public class Income
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public decimal? ReceivedRate { get; set; }
    public int SortOrder { get; set; }

    public BudgetContext Context { get; set; } = null!;
}
