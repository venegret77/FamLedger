namespace FamLedger.Domain.Models;

public record PeriodListItem(
    Guid Id,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsClosed,
    bool IsActive,
    decimal? Income,
    decimal? TopUps,
    decimal? PlannedExpenses,
    decimal? Spent,
    decimal? Remaining,
    int? TransactionCount,
    DateTime? ClosedAt);

public record CategoryBreakdownItem(string Name, decimal Amount, int Count);

public record DailyBreakdownItem(DateOnly Date, decimal Spent, decimal TopUps);

public record PeriodHistoryDetail(
    Guid Id,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsClosed,
    bool IsActive,
    decimal Income,
    decimal TopUps,
    decimal PlannedExpenses,
    decimal Spent,
    decimal Remaining,
    decimal DailyBudget,
    int DaysInPeriod,
    int TransactionCount,
    int ExpenseCount,
    int IncomeCount,
    DateTime? ClosedAt,
    IReadOnlyList<CategoryBreakdownItem> ByCategory,
    IReadOnlyList<DailyBreakdownItem> ByDay);
