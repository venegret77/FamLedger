using FamLedger.Domain.Enums;

namespace FamLedger.Domain.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContextId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; } = CategoryKind.Expense;
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }

    public BudgetContext Context { get; set; } = null!;
}
