namespace FamLedger.Domain.Entities;

public class ExchangeRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal RateToRsd { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
