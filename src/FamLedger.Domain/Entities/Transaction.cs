namespace FamLedger.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public decimal BaseAmount { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BudgetContext Context { get; set; } = null!;
    public BudgetPeriod Period { get; set; } = null!;
    public Category? Category { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
