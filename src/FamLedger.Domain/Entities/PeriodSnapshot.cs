namespace FamLedger.Domain.Entities;

/// <summary>
/// Frozen period totals for history and future AI analysis. Transactions stay in place.
/// </summary>
public class PeriodSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public Guid ContextId { get; set; }
    public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
    public Guid? ClosedByUserId { get; set; }

    public decimal Income { get; set; }
    public decimal TopUps { get; set; }
    public decimal PlannedExpenses { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining { get; set; }
    public decimal DailyBudget { get; set; }
    public int DaysInPeriod { get; set; }
    public int TransactionCount { get; set; }
    public int ExpenseCount { get; set; }
    public int IncomeCount { get; set; }

    /// <summary>JSON: [{ "name", "amount", "count" }]</summary>
    public string CategoryBreakdownJson { get; set; } = "[]";

    /// <summary>JSON: [{ "date", "spent", "topUps" }]</summary>
    public string DailyBreakdownJson { get; set; } = "[]";

    public BudgetPeriod Period { get; set; } = null!;
    public BudgetContext Context { get; set; } = null!;
    public User? ClosedByUser { get; set; }
}
