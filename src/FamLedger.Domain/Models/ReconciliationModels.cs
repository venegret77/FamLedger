namespace FamLedger.Domain.Models;

public record CurrencyAmount(string Currency, decimal Amount);

public record ReconciliationLine(
    string Key,
    string Label,
    bool IsManual,
    IReadOnlyList<CurrencyAmount> Amounts);

public record ReconciliationSide(
    IReadOnlyList<ReconciliationLine> Lines,
    IReadOnlyList<CurrencyAmount> Totals,
    decimal TotalBase);

public record ReconciliationSummary(
    decimal LedgerIncome,
    decimal LedgerExpenses,
    decimal LedgerTotal,
    decimal ActualTotal,
    decimal Difference);

public record ReconciliationView(
    Guid PeriodId,
    string PeriodLabel,
    string BaseCurrency,
    bool CanEdit,
    ReconciliationSide Assets,
    ReconciliationSide Obligations,
    ReconciliationSummary Summary,
    ReconciliationManualInput Manual);

public record ReconciliationManualInput(
    IReadOnlyDictionary<string, decimal> Cards,
    IReadOnlyDictionary<string, decimal> Cash,
    IReadOnlyDictionary<string, decimal> SetAside,
    IReadOnlyDictionary<string, decimal> ManualPlanned);
