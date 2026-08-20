using FamLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Repository;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BudgetContext> BudgetContexts => Set<BudgetContext>();
    public DbSet<ContextMember> ContextMembers => Set<ContextMember>();
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<PeriodRecurringItem> PeriodRecurringItems => Set<PeriodRecurringItem>();
    public DbSet<OneOffExpense> OneOffExpenses => Set<OneOffExpense>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtEntry> DebtEntries => Set<DebtEntry>();
    public DbSet<SavingsEntry> SavingsEntries => Set<SavingsEntry>();
    public DbSet<SavingsDeposit> SavingsDeposits => Set<SavingsDeposit>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalContribution> GoalContributions => Set<GoalContribution>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<RateOverride> RateOverrides => Set<RateOverride>();
    public DbSet<NotificationSubscription> NotificationSubscriptions => Set<NotificationSubscription>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TelegramUserId).IsUnique();
            e.Property(x => x.Username).HasMaxLength(128);
            e.Property(x => x.FirstName).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(256);
            e.Property(x => x.AvatarKey).HasMaxLength(512);
        });

        modelBuilder.Entity<BudgetContext>(e =>
        {
            e.ToTable("budget_contexts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.InviteCode).IsUnique();
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.BaseCurrency).HasMaxLength(8);
            e.Property(x => x.InviteCode).HasMaxLength(16);
        });

        modelBuilder.Entity<ContextMember>(e =>
        {
            e.ToTable("context_members");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ContextId, x.UserId }).IsUnique();
            e.HasOne(x => x.Context).WithMany(x => x.Members).HasForeignKey(x => x.ContextId);
            e.HasOne(x => x.User).WithMany(x => x.Memberships).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<JoinRequest>(e =>
        {
            e.ToTable("join_requests");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Context).WithMany(x => x.JoinRequests).HasForeignKey(x => x.ContextId);
            e.HasOne(x => x.User).WithMany(x => x.JoinRequests).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<BudgetPeriod>(e =>
        {
            e.ToTable("budget_periods");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ContextId, x.StartDate });
            e.Property(x => x.Label).HasMaxLength(64);
            e.HasOne(x => x.Context).WithMany(x => x.Periods).HasForeignKey(x => x.ContextId);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128);
            e.HasOne(x => x.Context).WithMany(x => x.Categories).HasForeignKey(x => x.ContextId);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PeriodId, x.Date });
            e.Property(x => x.Currency).HasMaxLength(8);
            e.HasOne(x => x.Context).WithMany(x => x.Transactions).HasForeignKey(x => x.ContextId);
            e.HasOne(x => x.Period).WithMany(x => x.Transactions).HasForeignKey(x => x.PeriodId);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
        });

        modelBuilder.Entity<RecurringExpense>(e =>
        {
            e.ToTable("recurring_expenses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.DefinitionCurrency).HasMaxLength(8);
            e.HasOne(x => x.Context).WithMany(x => x.RecurringExpenses).HasForeignKey(x => x.ContextId);
        });

        modelBuilder.Entity<PeriodRecurringItem>(e =>
        {
            e.ToTable("period_recurring_items");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PeriodId, x.RecurringExpenseId }).IsUnique();
            e.HasOne(x => x.Period).WithMany(x => x.RecurringItems).HasForeignKey(x => x.PeriodId);
            e.HasOne(x => x.RecurringExpense).WithMany(x => x.PeriodItems).HasForeignKey(x => x.RecurringExpenseId);
        });

        modelBuilder.Entity<OneOffExpense>(e =>
        {
            e.ToTable("one_off_expenses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasOne(x => x.Context).WithMany(x => x.OneOffExpenses).HasForeignKey(x => x.ContextId);
            e.HasOne(x => x.Period).WithMany(x => x.OneOffExpenses).HasForeignKey(x => x.PeriodId);
        });

        modelBuilder.Entity<Income>(e =>
        {
            e.ToTable("incomes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasOne(x => x.Context).WithMany(x => x.Incomes).HasForeignKey(x => x.ContextId);
        });

        modelBuilder.Entity<Debt>(e =>
        {
            e.ToTable("debts");
            e.HasKey(x => x.Id);
            e.Property(x => x.CounterpartyName).HasMaxLength(256);
            e.HasOne(x => x.Context).WithMany(x => x.Debts).HasForeignKey(x => x.ContextId);
            e.HasOne(x => x.CounterpartyUser).WithMany().HasForeignKey(x => x.CounterpartyUserId);
        });

        modelBuilder.Entity<DebtEntry>(e =>
        {
            e.ToTable("debt_entries");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Debt).WithMany(x => x.Entries).HasForeignKey(x => x.DebtId);
        });

        modelBuilder.Entity<SavingsEntry>(e =>
        {
            e.ToTable("savings_entries");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ContextId, x.PeriodId }).IsUnique();
            e.Property(x => x.PlannedCurrency).HasMaxLength(3);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasOne(x => x.Context).WithMany(x => x.SavingsEntries).HasForeignKey(x => x.ContextId);
            e.HasOne(x => x.Period).WithMany().HasForeignKey(x => x.PeriodId);
        });

        modelBuilder.Entity<SavingsDeposit>(e =>
        {
            e.ToTable("savings_deposits");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ContextId, x.PeriodId });
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasOne(x => x.Context).WithMany(x => x.SavingsDeposits).HasForeignKey(x => x.ContextId);
            e.HasOne(x => x.Period).WithMany().HasForeignKey(x => x.PeriodId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Goal>(e =>
        {
            e.ToTable("goals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasOne(x => x.Context).WithMany(x => x.Goals).HasForeignKey(x => x.ContextId);
        });

        modelBuilder.Entity<GoalContribution>(e =>
        {
            e.ToTable("goal_contributions");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Goal).WithMany(x => x.Contributions).HasForeignKey(x => x.GoalId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<ExchangeRate>(e =>
        {
            e.ToTable("exchange_rates");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Date, x.Currency }).IsUnique();
            e.Property(x => x.Currency).HasMaxLength(8);
        });

        modelBuilder.Entity<RateOverride>(e =>
        {
            e.ToTable("rate_overrides");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Context).WithMany().HasForeignKey(x => x.ContextId);
        });

        modelBuilder.Entity<NotificationSubscription>(e =>
        {
            e.ToTable("notification_subscriptions");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<WebhookEndpoint>(e =>
        {
            e.ToTable("webhook_endpoints");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Reminder>(e =>
        {
            e.ToTable("reminders");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.IsEnabled, x.TimeUtc });
            e.Property(x => x.Message).HasMaxLength(1000);
            e.HasOne(x => x.Context).WithMany(x => x.Reminders).HasForeignKey(x => x.ContextId);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
        });
    }
}
