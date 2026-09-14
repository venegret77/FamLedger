using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class CategorySpendingLimitService(
    AppDbContext db,
    IBudgetPeriodService periodService,
    INotificationService notifications) : ICategorySpendingLimitService
{
    public async Task<IReadOnlyList<CategorySpendingLimit>> ListAsync(
        Guid contextId,
        Guid userId,
        CancellationToken ct = default)
    {
        return await db.CategorySpendingLimits
            .AsNoTracking()
            .Include(l => l.Category)
            .Include(l => l.CreatedByUser)
            .Where(l =>
                l.ContextId == contextId &&
                (l.Audience == ReminderAudience.Family || l.CreatedByUserId == userId))
            .OrderBy(l => l.Category.Name)
            .ToListAsync(ct);
    }

    public async Task<CategorySpendingLimit> CreateAsync(
        Guid contextId,
        Guid userId,
        Guid categoryId,
        decimal limitAmount,
        IReadOnlyList<int>? thresholdPercents,
        ReminderAudience audience,
        bool isPersonalContext,
        CancellationToken ct = default)
    {
        ValidateAudience(audience, isPersonalContext);
        if (limitAmount <= 0)
            throw new InvalidOperationException("Limit amount must be positive");

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.ContextId == contextId, ct)
            ?? throw new InvalidOperationException("Category not found");
        if (category.Kind != CategoryKind.Expense)
            throw new InvalidOperationException("Limits are only for expense categories");

        var exists = await db.CategorySpendingLimits
            .AnyAsync(l => l.ContextId == contextId && l.CategoryId == categoryId, ct);
        if (exists)
            throw new InvalidOperationException("A limit for this category already exists");

        var limit = new CategorySpendingLimit
        {
            ContextId = contextId,
            CategoryId = categoryId,
            CreatedByUserId = userId,
            LimitAmount = decimal.Round(limitAmount, 2),
            ThresholdPercents = ThresholdPercentHelper.Normalize(
                thresholdPercents, ThresholdPercentHelper.DefaultCategoryLimit),
            Audience = audience,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        db.CategorySpendingLimits.Add(limit);
        await db.SaveChangesAsync(ct);

        return await db.CategorySpendingLimits
            .Include(l => l.Category)
            .Include(l => l.CreatedByUser)
            .FirstAsync(l => l.Id == limit.Id, ct);
    }

    public async Task<CategorySpendingLimit> UpdateAsync(
        Guid id,
        Guid userId,
        decimal limitAmount,
        IReadOnlyList<int>? thresholdPercents,
        ReminderAudience audience,
        bool isEnabled,
        bool isPersonalContext,
        CancellationToken ct = default)
    {
        ValidateAudience(audience, isPersonalContext);
        if (limitAmount <= 0)
            throw new InvalidOperationException("Limit amount must be positive");

        var limit = await db.CategorySpendingLimits.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new InvalidOperationException("Category limit not found");
        if (limit.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("Only the creator can edit this limit");

        limit.LimitAmount = decimal.Round(limitAmount, 2);
        limit.ThresholdPercents = ThresholdPercentHelper.Normalize(
            thresholdPercents, ThresholdPercentHelper.DefaultCategoryLimit);
        limit.Audience = audience;
        limit.IsEnabled = isEnabled;
        limit.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await db.CategorySpendingLimits
            .Include(l => l.Category)
            .Include(l => l.CreatedByUser)
            .FirstAsync(l => l.Id == limit.Id, ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var limit = await db.CategorySpendingLimits.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new InvalidOperationException("Category limit not found");
        if (limit.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("Only the creator can delete this limit");

        db.CategorySpendingLimits.Remove(limit);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CategoryLimitAlertInfo>> EvaluateAfterExpenseAsync(
        Guid contextId,
        Guid actingUserId,
        Guid? categoryId,
        bool notifyViaTelegram,
        CancellationToken ct = default)
    {
        if (categoryId is null) return [];

        var context = await db.BudgetContexts.FindAsync([contextId], ct);
        if (context is null) return [];

        var limits = await db.CategorySpendingLimits
            .Include(l => l.Category)
            .Include(l => l.CreatedByUser)
            .Where(l =>
                l.ContextId == contextId &&
                l.CategoryId == categoryId &&
                l.IsEnabled)
            .ToListAsync(ct);
        if (limits.Count == 0) return [];

        var period = await periodService.EnsureActivePeriodAsync(context, ct);
        var spent = await db.Transactions
            .Where(t =>
                t.PeriodId == period.Id &&
                t.CategoryId == categoryId &&
                t.Kind == TransactionKind.Expense)
            .SumAsync(t => t.BaseAmount, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var results = new List<CategoryLimitAlertInfo>();

        foreach (var limit in limits)
        {
            var appliesToActor = limit.Audience == ReminderAudience.Family
                || limit.CreatedByUserId == actingUserId;
            if (!appliesToActor) continue;
            if (limit.LimitAmount <= 0) continue;

            var percentUsed = (int)Math.Round(
                spent / limit.LimitAmount * 100m, MidpointRounding.AwayFromZero);
            var thresholds = ThresholdPercentHelper.Normalize(
                limit.ThresholdPercents, ThresholdPercentHelper.DefaultCategoryLimit);
            var active = thresholds.Where(t => percentUsed >= t).ToList();
            if (active.Count == 0) continue;

            var highest = active[^1];
            var categoryName = limit.Category.Name;
            var clientMessage = FormatCategoryLimitAlert(
                categoryName, spent, limit.LimitAmount, context.BaseCurrency, percentUsed, highest);
            results.Add(new CategoryLimitAlertInfo(
                clientMessage, limit.CategoryId, categoryName, percentUsed, highest,
                percentUsed >= 100));

            if (!notifyViaTelegram) continue;

            var firedToday = await db.CategoryLimitThresholdFires
                .AsNoTracking()
                .Where(f => f.LimitId == limit.Id && f.LastFiredDateUtc == today)
                .Select(f => f.ThresholdPercent)
                .ToListAsync(ct);
            var firedSet = firedToday.ToHashSet();
            var newlyCrossed = ThresholdPercentHelper.GetNewlyCrossed(thresholds, percentUsed, firedSet);
            if (newlyCrossed.Count == 0) continue;

            foreach (var threshold in newlyCrossed)
            {
                var message = FormatCategoryLimitAlert(
                    categoryName, spent, limit.LimitAmount, context.BaseCurrency, percentUsed, threshold);
                if (limit.Audience == ReminderAudience.Family)
                    await notifications.NotifyContextMembersAsync(limit.ContextId, message, ct);
                else if (limit.CreatedByUser is not null)
                    await notifications.SendTelegramAsync(limit.CreatedByUser.TelegramUserId, message, ct);
            }

            await MarkCategoryThresholdsFiredAsync(limit.Id, newlyCrossed, today, ct);
        }

        return results;
    }

    private async Task MarkCategoryThresholdsFiredAsync(
        Guid limitId,
        IEnumerable<int> thresholds,
        DateOnly todayUtc,
        CancellationToken ct)
    {
        var list = thresholds.Distinct().ToList();
        if (list.Count == 0) return;

        var existing = await db.CategoryLimitThresholdFires
            .Where(f => f.LimitId == limitId && list.Contains(f.ThresholdPercent))
            .ToListAsync(ct);

        foreach (var threshold in list)
        {
            var row = existing.FirstOrDefault(f => f.ThresholdPercent == threshold);
            if (row is null)
            {
                db.CategoryLimitThresholdFires.Add(new CategoryLimitThresholdFire
                {
                    LimitId = limitId,
                    ThresholdPercent = threshold,
                    LastFiredDateUtc = todayUtc,
                });
            }
            else
            {
                row.LastFiredDateUtc = todayUtc;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string FormatCategoryLimitAlert(
        string categoryName,
        decimal spent,
        decimal limitAmount,
        string currency,
        int percentUsed,
        int threshold)
    {
        var spentText = MoneyFormatter.Format(spent, currency);
        var limitText = MoneyFormatter.Format(limitAmount, currency);
        if (percentUsed >= 100)
        {
            return $"⚠️ Лимит категории «{categoryName}» исчерпан.\n" +
                   $"Потрачено: {spentText} из {limitText}";
        }

        return $"⚠️ Категория «{categoryName}»: {percentUsed}% лимита (порог {threshold}%).\n" +
               $"Потрачено: {spentText} из {limitText}";
    }

    private static void ValidateAudience(ReminderAudience audience, bool isPersonalContext)
    {
        if (audience == ReminderAudience.Family && isPersonalContext)
            throw new InvalidOperationException("Family audience is only available in a family budget");
    }
}
