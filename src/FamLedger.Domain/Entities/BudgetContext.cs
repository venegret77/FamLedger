namespace FamLedger.Domain.Entities;

public class BudgetContext
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsPersonal { get; set; }
    public string BaseCurrency { get; set; } = ValueObjects.CurrencyCode.Rsd;
    public int PeriodStartDay { get; set; } = 15;
    public string InviteCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ContextMember> Members { get; set; } = new List<ContextMember>();
    public ICollection<JoinRequest> JoinRequests { get; set; } = new List<JoinRequest>();
    public ICollection<BudgetPeriod> Periods { get; set; } = new List<BudgetPeriod>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<RecurringExpense> RecurringExpenses { get; set; } = new List<RecurringExpense>();
    public ICollection<OneOffExpense> OneOffExpenses { get; set; } = new List<OneOffExpense>();
    public ICollection<Income> Incomes { get; set; } = new List<Income>();
    public ICollection<Debt> Debts { get; set; } = new List<Debt>();
    public ICollection<SavingsEntry> SavingsEntries { get; set; } = new List<SavingsEntry>();
    public ICollection<SavingsDeposit> SavingsDeposits { get; set; } = new List<SavingsDeposit>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
}
