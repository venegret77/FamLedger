namespace FamLedger.Domain.Entities;

public class SavingsDeposit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BudgetContext Context { get; set; } = null!;
    public BudgetPeriod Period { get; set; } = null!;
    public User User { get; set; } = null!;
}
