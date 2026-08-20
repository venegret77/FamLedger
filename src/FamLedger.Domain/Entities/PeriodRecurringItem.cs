namespace FamLedger.Domain.Entities;

public class PeriodRecurringItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public Guid RecurringExpenseId { get; set; }
    public decimal PlannedBaseAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public bool IsSkipped { get; set; }

    public BudgetPeriod Period { get; set; } = null!;
    public RecurringExpense RecurringExpense { get; set; } = null!;
}
