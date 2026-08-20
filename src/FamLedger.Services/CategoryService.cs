using FamLedger.Common;
using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class CategoryService(AppDbContext db, IRedisService redis, IContextService contextService) : ICategoryService
{
    public async Task<IReadOnlyList<Category>> GetByContextAsync(Guid contextId, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.Categories(contextId);
        var cached = await redis.GetObjectAsync<List<Category>>(cacheKey);
        if (cached is not null) return cached;

        var list = await db.Categories
            .AsNoTracking()
            .Where(c => c.ContextId == contextId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new Category
            {
                Id = c.Id,
                ContextId = c.ContextId,
                Name = c.Name,
                Kind = c.Kind,
                SortOrder = c.SortOrder,
                IsDefault = c.IsDefault
            })
            .ToListAsync(ct);
        await redis.SetObjectAsync(cacheKey, list, TimeSpan.FromMinutes(30));
        return list;
    }

    public async Task<Category> CreateAsync(Guid contextId, string name, Guid userId, CancellationToken ct = default)
    {
        var member = await contextService.GetMembershipAsync(contextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        var cat = new Category { ContextId = contextId, Name = name, Kind = CategoryKind.Expense };
        db.Categories.Add(cat);
        await db.SaveChangesAsync(ct);
        await redis.DeleteAsync(CacheKeys.Categories(contextId));
        return cat;
    }

    public async Task UpdateAsync(Guid categoryId, string name, Guid userId, CancellationToken ct = default)
    {
        var cat = await db.Categories.FindAsync([categoryId], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(cat.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();
        cat.Name = name;
        await db.SaveChangesAsync(ct);
        await redis.DeleteAsync(CacheKeys.Categories(cat.ContextId));
    }

    public async Task DeleteAsync(Guid categoryId, Guid userId, CancellationToken ct = default)
    {
        var cat = await db.Categories.FindAsync([categoryId], ct) ?? throw new InvalidOperationException();
        var member = await contextService.GetMembershipAsync(cat.ContextId, userId, ct);
        if (member is null || !RolePermissions.CanManagePlan(member.Role))
            throw new UnauthorizedAccessException();

        await db.Transactions
            .Where(t => t.CategoryId == categoryId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CategoryId, (Guid?)null), ct);
        await db.RecurringExpenses
            .Where(r => r.CategoryId == categoryId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CategoryId, (Guid?)null), ct);

        db.Categories.Remove(cat);
        await db.SaveChangesAsync(ct);
        await redis.DeleteAsync(CacheKeys.Categories(cat.ContextId));
    }

    public async Task SeedDefaultsAsync(Guid contextId, CancellationToken ct = default)
    {
        if (await db.Categories.AnyAsync(c => c.ContextId == contextId, ct)) return;
        foreach (var (name, kind, order) in DefaultCategories.Items)
        {
            db.Categories.Add(new Category { ContextId = contextId, Name = name, Kind = kind, SortOrder = order, IsDefault = true });
        }
        await db.SaveChangesAsync(ct);
    }
}
