using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class ExpenseService(
    AppDbContext db,
    IBudgetPeriodService periodService,
    IExchangeRateService exchangeRateService,
    IRedisService redis) : IExpenseService
{
    public async Task<Transaction> AddAsync(Guid contextId, Guid userId, decimal amount, string currency, Guid? categoryId, string? note, DateOnly? date, CancellationToken ct = default)
    {
        var context = await db.BudgetContexts.FindAsync([contextId], ct) ?? throw new InvalidOperationException("Context not found");
        var period = await periodService.EnsureActivePeriodAsync(context, ct);
        var txDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var baseAmount = currency.Equals("RSD", StringComparison.OrdinalIgnoreCase)
            ? amount
            : await exchangeRateService.ConvertToBaseAsync(amount, currency, txDate, contextId, period.Id, ct);

        var tx = new Transaction
        {
            ContextId = contextId,
            PeriodId = period.Id,
            CategoryId = categoryId,
            CreatedByUserId = userId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            BaseAmount = baseAmount,
            Date = txDate,
            Note = note
        };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync(ct);
        await redis.DeleteAsync(CacheKeys.BudgetSummary(period.Id));
        return tx;
    }

    public Task<IReadOnlyList<Transaction>> GetByPeriodAsync(Guid periodId, CancellationToken ct = default) =>
        db.Transactions
            .Include(t => t.Category)
            .Include(t => t.CreatedByUser)
            .Where(t => t.PeriodId == periodId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Transaction>)t.Result, ct);

    public async Task DeleteAsync(Guid transactionId, Guid userId, CancellationToken ct = default)
    {
        var tx = await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transactionId, ct) ?? throw new InvalidOperationException();

        await db.Transactions
            .Where(t => t.Id == transactionId)
            .ExecuteDeleteAsync(ct);
        await redis.DeleteAsync(CacheKeys.BudgetSummary(tx.PeriodId));
    }
}
