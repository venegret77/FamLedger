namespace FamLedger.Domain.Entities;

public class DebtEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DebtId { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public string Description { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal RemainingAmount => Math.Max(0m, Amount - PaidAmount);

    public Debt Debt { get; set; } = null!;

    public void ApplyPayment(decimal amount)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount));

        PaidAmount = Math.Min(Amount, PaidAmount + amount);
        IsPaid = PaidAmount >= Amount;
    }

    public void ClearPayment()
    {
        PaidAmount = 0m;
        IsPaid = false;
    }
}
