namespace FamLedger.Domain.Models;

public record CategoryLimitProgress(
    string CategoryName,
    decimal Spent,
    decimal LimitAmount,
    int PercentUsed);
