namespace FamLedger.Domain.Models;

public readonly record struct ParsedMoneyInput(decimal Amount, string Currency, string? Remainder);
