using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class ExpenseService(
    AppDbContext db,
    IBudgetPeriodService periodService,
    IExchangeRateService exchangeRateService,
    IRedisService redis,
    IUserActivityService userActivity,
    IBudgetAlertService budgetAlertService,
    ICategorySpendingLimitService categoryLimitService) : IExpenseService
{
    public async Task<Transaction> AddAsync(
        Guid contextId,
        Guid userId,
        decimal amount,
        string currency,
        Guid? categoryId,
        string? note,
        DateOnly? date,
        TransactionKind kind = TransactionKind.Expense,
        CancellationToken ct = default)
    {
        var context = await db.BudgetContexts.FirstOrDefaultAsync(
            context => context.Id == contextId && context.Members.Any(member => member.UserId == userId), ct)
            ?? throw new UnauthorizedAccessException();
        if (categoryId is { } id)
        {
            var categoryKind = kind == TransactionKind.Income ? CategoryKind.Income : CategoryKind.Expense;
            var validCategory = await db.Categories.AnyAsync(
                category => category.Id == id && category.ContextId == contextId && category.Kind == categoryKind, ct);
            if (!validCategory)
                throw new ArgumentException("Выбери категорию этого бюджета для данного типа операции.");
        }
        var period = await periodService.EnsureActivePeriodAsync(context, ct);
        var txDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var baseAmount = currency.Equals(context.BaseCurrency, StringComparison.OrdinalIgnoreCase)
            ? amount
            : await exchangeRateService.ConvertToBaseAsync(amount, currency, txDate, contextId, period.Id, ct);

        var tx = new Transaction
        {
            ContextId = contextId,
            PeriodId = period.Id,
            CategoryId = categoryId,
            CreatedByUserId = userId,
            Kind = kind,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            BaseAmount = baseAmount,
            Date = txDate,
            Note = note
        };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync(ct);
        await redis.DeleteAsync(CacheKeys.BudgetSummary(period.Id));
        await userActivity.TouchAsync(userId, contextId, UserActivityKind.Transaction, ct);
        return tx;
    }

    public async Task<IReadOnlyList<Transaction>> GetPageAsync(Guid periodId, int offset, int limit, CancellationToken ct = default) =>
        await db.Transactions.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.CreatedByUser)
            .Where(t => t.PeriodId == periodId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

    public async Task DeleteAsync(Guid transactionId, Guid userId, CancellationToken ct = default)
    {
        var transactions = db.Transactions.Where(tx => tx.Id == transactionId
            && tx.Context.Members.Any(member => member.UserId == userId));
        var tx = await transactions
            .AsNoTracking()
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();

        if (await transactions.ExecuteDeleteAsync(ct) == 0)
            throw new KeyNotFoundException();
        await redis.DeleteAsync(CacheKeys.BudgetSummary(tx.PeriodId));

        if (tx.Kind == TransactionKind.Expense)
        {
            await budgetAlertService.ReconcileFiresAfterSpendChangeAsync(tx.ContextId, ct);
            await categoryLimitService.ReconcileFiresAfterSpendChangeAsync(tx.ContextId, tx.CategoryId, ct);
        }
    }
}
