namespace FamLedger.Domain.Entities;

public class RecurringExpense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public decimal DefinitionAmount { get; set; }
    public string DefinitionCurrency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public int ChargeDayOfMonth { get; set; } = 1;
    public bool RecalcByRate { get; set; }
    public Guid? SourceIncomeId { get; set; }

    public BudgetContext Context { get; set; } = null!;
    public Category? Category { get; set; }
    public ICollection<PeriodRecurringItem> PeriodItems { get; set; } = new List<PeriodRecurringItem>();
}
