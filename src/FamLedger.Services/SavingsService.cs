using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class SavingsService(
    AppDbContext db,
    IContextService contextService,
    IExchangeRateService exchangeRateService) : ISavingsService
{
    public async Task<SavingsEntry> GetOrCreateForPeriodAsync(Guid contextId, Guid periodId, CancellationToken ct = default)
    {
        var entry = await db.SavingsEntries.FirstOrDefaultAsync(s => s.ContextId == contextId && s.PeriodId == periodId, ct);
        if (entry is not null) return entry;

        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        entry = new SavingsEntry
        {
            ContextId = contextId,
            PeriodId = periodId,
            Currency = context.BaseCurrency,
            PlannedCurrency = context.BaseCurrency
        };
        db.SavingsEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task AddDepositAsync(
        Guid contextId,
        Guid periodId,
        decimal amount,
        string currency,
        Guid userId,
        CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        await AddMovementAsync(contextId, periodId, amount, currency, userId, ct);
    }

    public async Task WithdrawAsync(
        Guid contextId,
        Guid periodId,
        decimal amount,
        string currency,
        Guid userId,
        CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        var withdrawBase = await ToBaseAtCurrentRateAsync(contextId, periodId, amount, currency, ct);
        var balance = await GetTotalBalanceAsync(contextId, ct);
        if (withdrawBase > balance)
            throw new InvalidOperationException("Недостаточно средств в копилке.");

        await AddMovementAsync(contextId, periodId, -amount, currency, userId, ct);
    }

    private async Task AddMovementAsync(
        Guid contextId,
        Guid periodId,
        decimal signedAmount,
        string currency,
        Guid userId,
        CancellationToken ct)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        var code = string.IsNullOrWhiteSpace(currency)
            ? context.BaseCurrency
            : currency.ToUpperInvariant();

        await GetOrCreateForPeriodAsync(contextId, periodId, ct);

        db.SavingsDeposits.Add(new SavingsDeposit
        {
            ContextId = contextId,
            PeriodId = periodId,
            UserId = userId,
            Amount = signedAmount,
            Currency = code
        });

        var entry = await db.SavingsEntries.FirstAsync(s => s.ContextId == contextId && s.PeriodId == periodId, ct);
        entry.ActualAmount = await SumDepositsInBaseAsync(contextId, periodId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetPlanAsync(
        Guid contextId,
        Guid periodId,
        decimal plannedAmount,
        string currency,
        Guid userId,
        CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        var code = string.IsNullOrWhiteSpace(currency)
            ? context.BaseCurrency
            : currency.ToUpperInvariant();

        var entry = await GetOrCreateForPeriodAsync(contextId, periodId, ct);
        entry.PlannedAmount = plannedAmount;
        entry.PlannedCurrency = code;
        await db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetTotalBalanceAsync(Guid contextId, CancellationToken ct = default)
    {
        var deposits = await db.SavingsDeposits
            .Where(d => d.ContextId == contextId)
            .Select(d => new { d.Amount, d.Currency, d.PeriodId })
            .ToListAsync(ct);

        decimal total = 0;
        foreach (var deposit in deposits)
            total += await ToBaseAtCurrentRateAsync(contextId, deposit.PeriodId, deposit.Amount, deposit.Currency, ct);
        return total;
    }

    public async Task<IReadOnlyList<SavingsMovementView>> GetMovementsAsync(
        Guid contextId,
        CancellationToken ct = default)
    {
        var deposits = await db.SavingsDeposits
            .AsNoTracking()
            .Include(d => d.Period)
            .Include(d => d.User)
            .Where(d => d.ContextId == contextId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return deposits.Select(d => new SavingsMovementView(
            d.Id,
            d.PeriodId,
            d.Period?.Label,
            d.Amount,
            d.Currency,
            d.CreatedAt,
            d.User.DisplayName ?? d.User.FirstName)).ToList();
    }

    public async Task DeleteDepositAsync(
        Guid contextId,
        Guid depositId,
        Guid userId,
        CancellationToken ct = default)
    {
        var deposit = await db.SavingsDeposits
            .FirstOrDefaultAsync(d => d.Id == depositId && d.ContextId == contextId, ct)
            ?? throw new InvalidOperationException("Операция не найдена.");

        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var periodId = deposit.PeriodId;

        db.SavingsDeposits.Remove(deposit);

        var entry = await db.SavingsEntries
            .FirstOrDefaultAsync(s => s.ContextId == contextId && s.PeriodId == periodId, ct);
        if (entry is not null)
            entry.ActualAmount = await SumDepositsInBaseAsync(contextId, periodId, ct);

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SavingsPeriodView>> GetPlansAsync(Guid contextId, CancellationToken ct = default)
    {
        var entries = await db.SavingsEntries
            .Include(s => s.Period)
            .Where(s => s.ContextId == contextId)
            .OrderBy(s => s.Period!.StartDate)
            .ToListAsync(ct);

        var deposits = await db.SavingsDeposits
            .Where(d => d.ContextId == contextId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

        var result = new List<SavingsPeriodView>();
        foreach (var entry in entries)
        {
            var periodDeposits = deposits.Where(d => d.PeriodId == entry.PeriodId).ToList();
            var byCurrency = periodDeposits
                .GroupBy(d => d.Currency)
                .Select(g => new SavingsAmountByCurrency(g.Sum(x => x.Amount), g.Key))
                .Where(x => x.Amount != 0)
                .OrderBy(x => x.Currency)
                .ToList();

            var actualBase = 0m;
            foreach (var deposit in periodDeposits)
            {
                actualBase += await ToBaseAtCurrentRateAsync(
                    contextId, entry.PeriodId, deposit.Amount, deposit.Currency, ct);
            }

            var plannedBase = await ToBaseAtCurrentRateAsync(
                contextId, entry.PeriodId, entry.PlannedAmount, entry.PlannedCurrency, ct);

            result.Add(new SavingsPeriodView(
                entry.Id,
                entry.PlannedAmount,
                entry.PlannedCurrency,
                plannedBase,
                actualBase,
                entry.Currency,
                entry.Period?.Label,
                entry.Period?.StartDate,
                entry.Period?.EndDate,
                byCurrency));
        }

        return result;
    }

    private async Task<decimal> SumDepositsInBaseAsync(Guid contextId, Guid periodId, CancellationToken ct)
    {
        var deposits = await db.SavingsDeposits
            .Where(d => d.ContextId == contextId && d.PeriodId == periodId)
            .Select(d => new { d.Amount, d.Currency })
            .ToListAsync(ct);

        decimal total = 0;
        foreach (var deposit in deposits)
            total += await ToBaseAtCurrentRateAsync(contextId, periodId, deposit.Amount, deposit.Currency, ct);
        return total;
    }

    private async Task<decimal> ToBaseAtCurrentRateAsync(
        Guid contextId,
        Guid periodId,
        decimal amount,
        string currency,
        CancellationToken ct)
    {
        var context = await db.BudgetContexts.FindAsync([contextId], ct)
            ?? throw new InvalidOperationException("Context not found.");
        var code = string.IsNullOrWhiteSpace(currency)
            ? context.BaseCurrency
            : currency.ToUpperInvariant();

        if (code.Equals(context.BaseCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await exchangeRateService.ConvertToBaseAsync(
            amount, code, today, contextId, periodId, ct);
    }
}
