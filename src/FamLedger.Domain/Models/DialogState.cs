namespace FamLedger.Domain.Models;

public class DialogState
{
    public string Step { get; set; } = string.Empty;
    /// <summary>expense | income | debt</summary>
    public string? Intent { get; set; }
    public decimal? PendingAmount { get; set; }
    public string? PendingCurrency { get; set; }
    public string? PendingNote { get; set; }
    public Guid? PendingContextId { get; set; }
    public Guid? PendingDebtId { get; set; }
    public string? PendingDebtName { get; set; }
}
