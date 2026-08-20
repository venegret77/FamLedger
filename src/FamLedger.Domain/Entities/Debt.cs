using FamLedger.Domain.Enums;

namespace FamLedger.Domain.Entities;

public class Debt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public Guid? CounterpartyUserId { get; set; }
    public DebtDirection Direction { get; set; }

    public BudgetContext Context { get; set; } = null!;
    public User? CounterpartyUser { get; set; }
    public ICollection<DebtEntry> Entries { get; set; } = new List<DebtEntry>();
}
