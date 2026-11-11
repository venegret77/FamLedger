using Microsoft.EntityFrameworkCore;

namespace FamLedger.Repository;

public static class BudgetWriteLock
{
    public static async Task LockBudgetAsync(this AppDbContext db, Guid contextId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A budget write lock requires a transaction.");

        var rows = await db.Database.SqlQuery<Guid>(
            $"""SELECT "Id" AS "Value" FROM budget_contexts WHERE "Id" = {contextId} FOR UPDATE""")
            .ToListAsync(ct);
        if (rows.Count == 0)
            throw new InvalidOperationException("Budget not found.");
    }
}
