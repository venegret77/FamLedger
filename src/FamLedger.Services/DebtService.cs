using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class DebtService(AppDbContext db) : IDebtService
{
    public async Task<Debt> CreateAsync(Guid contextId, string counterpartyName, Guid? counterpartyUserId, DebtDirection direction, CancellationToken ct = default)
    {
        var debt = new Debt
        {
            ContextId = contextId,
            CounterpartyName = counterpartyName,
            CounterpartyUserId = counterpartyUserId,
            Direction = direction
        };
        db.Debts.Add(debt);
        await db.SaveChangesAsync(ct);
        return debt;
    }

    public async Task<DebtEntry> AddEntryAsync(Guid debtId, decimal amount, string currency, string description, CancellationToken ct = default)
    {
        var entry = new DebtEntry
        {
            DebtId = debtId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            Description = description
        };
        db.DebtEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task ToggleEntryPaidAsync(Guid entryId, CancellationToken ct = default)
    {
        var entry = await db.DebtEntries.FindAsync([entryId], ct) ?? throw new InvalidOperationException();
        entry.IsPaid = !entry.IsPaid;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid debtId, CancellationToken ct = default)
    {
        var exists = await db.Debts.AnyAsync(d => d.Id == debtId, ct);
        if (!exists) throw new InvalidOperationException();

        await db.DebtEntries
            .Where(e => e.DebtId == debtId)
            .ExecuteDeleteAsync(ct);
        await db.Debts
            .Where(d => d.Id == debtId)
            .ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        var entry = await db.DebtEntries.FindAsync([entryId], ct) ?? throw new InvalidOperationException();
        db.DebtEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Debt>> GetByContextAsync(Guid contextId, bool hidePaid, CancellationToken ct = default)
    {
        var query = db.Debts
            .Include(d => d.Entries)
            .Include(d => d.CounterpartyUser)
            .Where(d => d.ContextId == contextId);

        var debts = await query.ToListAsync(ct);
        if (hidePaid)
        {
            debts = debts.Where(d => d.Entries.Any(e => !e.IsPaid)).ToList();
        }
        return debts;
    }
}
