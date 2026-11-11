using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Npgsql;
using Xunit;

namespace FamLedger.Services.Tests;

public sealed class PostgresAuditTests(PostgresAuditFixture fixture) : IClassFixture<PostgresAuditFixture>
{
    [PostgresFact]
    public async Task DebtMutationsRequireMembershipAndTheSelectedBudget()
    {
        await using var db = fixture.Open();
        var (family, member) = await SeedBudgetAsync(db);
        var (otherFamily, otherMember) = await SeedBudgetAsync(db);
        var service = new DebtService(db);
        var debt = await service.CreateAsync(family.Id, member.UserId, "Synthetic debt", null, DebtDirection.WeOwe);
        var entry = await service.AddEntryAsync(family.Id, member.UserId, debt.Id, 100, "RSD", "");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AddEntryAsync(otherFamily.Id, otherMember.UserId, debt.Id, 100, "RSD", ""));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApplyEntryPaymentAsync(family.Id, otherMember.UserId, entry.Id, 50));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ToggleEntryPaidAsync(otherFamily.Id, member.UserId, entry.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteEntryAsync(otherFamily.Id, otherMember.UserId, entry.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteAsync(family.Id, otherMember.UserId, debt.Id));

        await service.ApplyEntryPaymentAsync(family.Id, member.UserId, entry.Id, 50);
        Assert.Equal(50m, entry.RemainingAmount);
        await service.DeleteAsync(family.Id, member.UserId, debt.Id);
        Assert.False(await db.DebtEntries.AnyAsync(item => item.Id == entry.Id));
    }

    [PostgresFact]
    public async Task ConcurrentWithdrawalsCannotSpendTheSameBalance()
    {
        await using var db = fixture.Open();
        var (family, member) = await SeedBudgetAsync(db);
        var period = await NewPeriodService(db).EnsureActivePeriodAsync(family);
        var rates = Mock.Of<IExchangeRateService>();
        var savings = new SavingsService(db, new ContextService(db), rates);
        await savings.AddDepositAsync(family.Id, period.Id, 100, "RSD", member.UserId);

        var results = await Task.WhenAll(WithdrawAsync(), WithdrawAsync());
        Assert.Single(results, success => success);
        Assert.Equal(30m, await savings.GetTotalBalanceAsync(family.Id));
        var entry = await db.SavingsEntries.AsNoTracking().SingleAsync(row => row.ContextId == family.Id);
        Assert.Equal(30m, entry.ActualAmount);

        async Task<bool> WithdrawAsync()
        {
            await using var session = fixture.Open();
            var service = new SavingsService(session, new ContextService(session), rates);
            try
            {
                await service.WithdrawAsync(family.Id, period.Id, 70, "RSD", member.UserId);
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message == "Недостаточно средств в копилке.")
            {
                return false;
            }
        }
    }

    [PostgresFact]
    public async Task ConcurrentPeriodCreationReturnsOnePeriodAndCopiesThePlanOnce()
    {
        await using var db = fixture.Open();
        var (family, _) = await SeedBudgetAsync(db);
        db.RecurringExpenses.Add(new RecurringExpense
        {
            ContextId = family.Id, Name = "Synthetic recurring cost", DefinitionCurrency = "RSD", DefinitionAmount = 25
        });
        await db.SaveChangesAsync();

        var periods = await Task.WhenAll(OpenAsync(), OpenAsync());
        Assert.Equal(periods[0].Id, periods[1].Id);
        Assert.Equal(1, await db.PeriodRecurringItems.CountAsync(item => item.PeriodId == periods[0].Id));
        Assert.Equal(1, await db.BudgetPeriods.CountAsync(period => period.ContextId == family.Id && !period.IsClosed));

        async Task<BudgetPeriod> OpenAsync()
        {
            await using var session = fixture.Open();
            return await NewPeriodService(session).EnsureActivePeriodAsync(family);
        }
    }

    [PostgresFact]
    public async Task UpgradePreservesExistingOpenPeriodsAndTheirTransactions()
    {
        var legacy = new PostgresAuditFixture();
        try
        {
            await using var db = legacy.Open();
            await db.GetService<IMigrator>().MigrateAsync("20261026193000_ReminderSchedules");
            var (budget, member) = await SeedBudgetAsync(db);
            var first = new BudgetPeriod
            {
                ContextId = budget.Id, Label = "First",
                StartDate = new DateOnly(2026, 1, 15), EndDate = new DateOnly(2026, 2, 14)
            };
            var second = new BudgetPeriod
            {
                ContextId = budget.Id, Label = "Second",
                StartDate = new DateOnly(2026, 2, 15), EndDate = new DateOnly(2026, 3, 14)
            };
            db.AddRange(first, second);
            db.Transactions.AddRange(
                new Transaction { ContextId = budget.Id, PeriodId = first.Id, CreatedByUserId = member.UserId,
                    Date = first.StartDate, Amount = 20, BaseAmount = 20 },
                new Transaction { ContextId = budget.Id, PeriodId = second.Id, CreatedByUserId = member.UserId,
                    Date = second.StartDate, Amount = 30, BaseAmount = 30 });
            await db.SaveChangesAsync();

            await db.Database.MigrateAsync();
            Assert.Equal(2, await db.BudgetPeriods.CountAsync(period => period.ContextId == budget.Id && !period.IsClosed));
            var rates = Mock.Of<IExchangeRateService>();
            var periods = new BudgetPeriodService(db, rates, new BudgetCalculatorService(db, rates), new ContextService(db));
            Assert.Equal(first.Id, (await periods.EnsureActivePeriodAsync(budget)).Id);
            Assert.Equal(second.Id, (await periods.CloseActivePeriodAsync(budget.Id, member.UserId)).Id);
            Assert.Equal(2, await db.BudgetPeriods.CountAsync(period => period.ContextId == budget.Id));
            Assert.Equal(20m, await db.Transactions.Where(tx => tx.PeriodId == first.Id).SumAsync(tx => tx.Amount));
            Assert.Equal(30m, await db.Transactions.Where(tx => tx.PeriodId == second.Id).SumAsync(tx => tx.Amount));
        }
        finally
        {
            await legacy.DisposeAsync();
        }
    }

    [PostgresFact]
    public async Task LastHeadCannotBeDemotedAndRemovedMembersLosePersonalReminders()
    {
        await using var db = fixture.Open();
        var (family, head) = await SeedBudgetAsync(db);
        var guest = new User { TelegramUserId = Random.Shared.NextInt64(1, long.MaxValue) };
        var membership = new ContextMember { ContextId = family.Id, User = guest, Role = FamilyMemberRole.Member };
        var reminder = new Reminder { ContextId = family.Id, CreatedByUser = guest, Audience = ReminderAudience.Self };
        db.AddRange(membership, reminder);
        await db.SaveChangesAsync();

        var service = new ContextService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateMemberRoleAsync(family.Id, head.Id, FamilyMemberRole.Member, head.UserId));
        await service.RemoveMemberAsync(family.Id, membership.Id, head.UserId);
        Assert.False(await db.Reminders.Where(row => row.Id == reminder.Id).Select(row => row.IsEnabled).SingleAsync());
    }

    [PostgresFact]
    public async Task LateReminderIsReturnedOnceAndAdvancesToItsNextOccurrence()
    {
        await using var db = fixture.Open();
        var (family, head) = await SeedBudgetAsync(db);
        var now = new DateTime(2026, 11, 11, 10, 37, 0, DateTimeKind.Utc);
        var reminder = new Reminder
        {
            ContextId = family.Id, CreatedByUserId = head.UserId,
            Kind = ReminderKind.Custom, TimeUtc = new TimeOnly(10, 0),
            CreatedAtUtc = now.AddDays(-1), UpdatedAtUtc = now.AddDays(-1)
        };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();
        var service = new ReminderService(db);

        var due = Assert.Single(await service.GetDueTimedAsync(now), row => row.Id == reminder.Id);
        Assert.Equal(now.AddMinutes(-37), due.NextDueAtUtc);
        await service.CompleteTimedAsync(due, fired: true);
        Assert.DoesNotContain(await service.GetDueTimedAsync(now.AddMinutes(1)), row => row.Id == reminder.Id);
        Assert.Contains(await service.GetDueTimedAsync(now.AddDays(1)), row => row.Id == reminder.Id);
    }

    [PostgresFact]
    public async Task ExpenseCategoryAndDeletionAreScopedToTheUsersBudget()
    {
        await using var db = fixture.Open();
        var (family, member) = await SeedBudgetAsync(db);
        var (otherFamily, otherMember) = await SeedBudgetAsync(db);
        var wrongCategory = new Category { ContextId = otherFamily.Id, Kind = CategoryKind.Expense, Name = "Synthetic" };
        var incomeCategory = new Category { ContextId = family.Id, Kind = CategoryKind.Income, Name = "Income" };
        db.AddRange(wrongCategory, incomeCategory);
        await db.SaveChangesAsync();

        var expense = new ExpenseService(db, NewPeriodService(db), Mock.Of<IExchangeRateService>(),
            Mock.Of<IRedisService>(), Mock.Of<IUserActivityService>(),
            Mock.Of<IBudgetAlertService>(), Mock.Of<ICategorySpendingLimitService>());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            expense.AddAsync(family.Id, member.UserId, 30, "RSD", wrongCategory.Id, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            expense.AddAsync(family.Id, member.UserId, 30, "RSD", incomeCategory.Id, null, null));

        var transaction = await expense.AddAsync(family.Id, member.UserId, 30, "RSD", null, null, null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => expense.DeleteAsync(transaction.Id, otherMember.UserId));
        Assert.True(await db.Transactions.AnyAsync(row => row.Id == transaction.Id));
    }

    private static async Task<(BudgetContext Budget, ContextMember Head)> SeedBudgetAsync(AppDbContext db)
    {
        var budget = new BudgetContext { Name = "Synthetic budget", InviteCode = Guid.NewGuid().ToString("N")[..12] };
        var user = new User { TelegramUserId = Random.Shared.NextInt64(1, long.MaxValue) };
        var member = new ContextMember { Context = budget, User = user, Role = FamilyMemberRole.Head };
        db.ContextMembers.Add(member);
        await db.SaveChangesAsync();
        return (budget, member);
    }

    private static BudgetPeriodService NewPeriodService(AppDbContext db) =>
        new(db, Mock.Of<IExchangeRateService>(), Mock.Of<IBudgetCalculatorService>(), new ContextService(db));
}

public sealed class PostgresAuditFixture : IAsyncLifetime
{
    private const string Variable = "FAMLEDGER_TEST_POSTGRES";
    private readonly DbContextOptions<AppDbContext>? _options = CreateOptions();

    public AppDbContext Open() => new(_options ?? throw new InvalidOperationException("No test database configured."));
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable));

    public async Task InitializeAsync()
    {
        if (_options is null) return;
        await using var db = Open();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_options is null) return;
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
    }

    private static DbContextOptions<AppDbContext>? CreateOptions()
    {
        if (!IsConfigured) return null;
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(Variable));
        if (connection.Host is not ("127.0.0.1" or "localhost"))
            throw new InvalidOperationException("Integration tests require a local PostgreSQL server.");

        connection.Database = $"famledger_test_{Guid.NewGuid():N}";
        return new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresAuditFixture.IsConfigured)
            Skip = "Set FAMLEDGER_TEST_POSTGRES to a dedicated local PostgreSQL server.";
    }
}
