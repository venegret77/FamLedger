namespace FamLedger.Domain.Models;

public class DialogState
{
    public string Step { get; set; } = string.Empty;
    public decimal? PendingAmount { get; set; }
    public string? PendingCurrency { get; set; }
    public string? PendingNote { get; set; }
    public Guid? PendingContextId { get; set; }
}
