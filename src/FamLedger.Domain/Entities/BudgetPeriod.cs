namespace FamLedger.Domain.Entities;

public class BudgetPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal CarryoverBase { get; set; }
    public bool IsClosed { get; set; }

    public BudgetContext Context { get; set; } = null!;
    public ICollection<PeriodRecurringItem> RecurringItems { get; set; } = new List<PeriodRecurringItem>();
    public ICollection<OneOffExpense> OneOffExpenses { get; set; } = new List<OneOffExpense>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
