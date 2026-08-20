namespace FamLedger.Domain.Entities;

public class DebtEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DebtId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public string Description { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Debt Debt { get; set; } = null!;
}
