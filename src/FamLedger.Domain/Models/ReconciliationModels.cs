namespace FamLedger.Domain.Models;

public record CurrencyAmount(string Currency, decimal Amount);

public record ReconciliationManualEntry(
    Guid Id,
    string Name,
    decimal Amount,
    string Currency);

public record ReconciliationLine(
    string Key,
    string Label,
    bool IsManual,
    IReadOnlyList<CurrencyAmount> Amounts,
    Guid? EntryId = null);

public record ReconciliationSide(
    IReadOnlyList<ReconciliationLine> Lines,
    IReadOnlyList<CurrencyAmount> Totals,
    decimal TotalBase);

public record ReconciliationSummary(
    decimal LedgerIncome,
    decimal LedgerExpenses,
    decimal LedgerTotal,
    decimal AssetTotal,
    decimal ObligationTotal,
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
    IReadOnlyList<ReconciliationManualEntry> AssetItems,
    IReadOnlyList<ReconciliationManualEntry> ObligationItems);
