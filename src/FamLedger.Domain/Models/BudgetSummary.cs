namespace FamLedger.Domain.Models;

public class BudgetSummary
{
    public decimal Income { get; set; }
    public decimal PlannedExpenses { get; set; }
    public decimal Spent { get; set; }
    public decimal Carryover { get; set; }
    public decimal Remaining { get; set; }
    public decimal DailyBudgetAtStart { get; set; }
    public decimal DailyBudgetNow { get; set; }
    public decimal AvailableToday { get; set; }
    public decimal SpentToday { get; set; }
    public int DaysInPeriod { get; set; }
    public int DaysPassed { get; set; }
    public int DaysRemaining { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public Guid PeriodId { get; set; }
}
