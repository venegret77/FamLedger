using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using FamLedger.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FamLedger.Services.Tests;

public class CategoryServiceReorderTests
{
    [Fact]
    public async Task ReorderAsync_Should_UpdateSortOrder_When_AllIdsProvided()
    {
        var contextId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        await using var db = CreateDb();
        db.Categories.AddRange(
            new Category { Id = a, ContextId = contextId, Name = "A", SortOrder = 0 },
            new Category { Id = b, ContextId = contextId, Name = "B", SortOrder = 1 },
            new Category { Id = c, ContextId = contextId, Name = "C", SortOrder = 2 });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, contextId, userId, FamilyMemberRole.Head);
        await sut.ReorderAsync(contextId, userId, [c, a, b]);

        var ordered = await db.Categories
            .Where(x => x.ContextId == contextId)
            .OrderBy(x => x.SortOrder)
            .Select(x => x.Id)
            .ToListAsync();

        Assert.Equal([c, a, b], ordered);
    }

    [Fact]
    public async Task ReorderAsync_Should_Throw_When_ListIncomplete()
    {
        var contextId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await using var db = CreateDb();
        db.Categories.AddRange(
            new Category { Id = a, ContextId = contextId, Name = "A", SortOrder = 0 },
            new Category { Id = b, ContextId = contextId, Name = "B", SortOrder = 1 });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, contextId, userId, FamilyMemberRole.Head);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ReorderAsync(contextId, userId, [a]));
    }

    [Fact]
    public async Task ReorderAsync_Should_Throw_When_Duplicates()
    {
        var contextId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await using var db = CreateDb();
        db.Categories.AddRange(
            new Category { Id = a, ContextId = contextId, Name = "A", SortOrder = 0 },
            new Category { Id = b, ContextId = contextId, Name = "B", SortOrder = 1 });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, contextId, userId, FamilyMemberRole.Head);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ReorderAsync(contextId, userId, [a, a]));
    }

    [Fact]
    public async Task ReorderAsync_Should_Forbid_When_NoPlanPermission()
    {
        var contextId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var a = Guid.NewGuid();

        await using var db = CreateDb();
        db.Categories.Add(new Category { Id = a, ContextId = contextId, Name = "A", SortOrder = 0 });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, contextId, userId, FamilyMemberRole.Member);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.ReorderAsync(contextId, userId, [a]));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CategoryService CreateSut(
        AppDbContext db,
        Guid contextId,
        Guid userId,
        FamilyMemberRole role)
    {
        var redis = new Mock<IRedisService>();
        redis.Setup(r => r.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var contexts = new Mock<IContextService>();
        contexts
            .Setup(c => c.GetMembershipAsync(contextId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextMember
            {
                ContextId = contextId,
                UserId = userId,
                Role = role,
            });

        return new CategoryService(db, redis.Object, contexts.Object);
    }
}
