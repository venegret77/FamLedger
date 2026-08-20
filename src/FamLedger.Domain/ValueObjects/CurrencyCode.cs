namespace FamLedger.Domain.ValueObjects;

public static class CurrencyCode
{
    public const string Rsd = "RSD";
    public const string Eur = "EUR";
    public const string Usd = "USD";

    public static readonly string[] All = [Rsd, Eur, Usd];

    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        All.Contains(code.ToUpperInvariant());
}
