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
        var existing = await FindExistingAsync(contextId, counterpartyName, counterpartyUserId, direction, ct);
        if (existing is not null)
            return existing;

        var debt = new Debt
        {
            ContextId = contextId,
            CounterpartyName = counterpartyName.Trim(),
            CounterpartyUserId = counterpartyUserId,
            Direction = direction
        };
        db.Debts.Add(debt);
        await db.SaveChangesAsync(ct);
        return debt;
    }

    private async Task<Debt?> FindExistingAsync(
        Guid contextId,
        string counterpartyName,
        Guid? counterpartyUserId,
        DebtDirection direction,
        CancellationToken ct)
    {
        var name = counterpartyName.Trim();
        var query = db.Debts.Where(d => d.ContextId == contextId && d.Direction == direction);

        if (counterpartyUserId is not null)
        {
            return await query.FirstOrDefaultAsync(d => d.CounterpartyUserId == counterpartyUserId, ct);
        }

        return await query.FirstOrDefaultAsync(
            d => d.CounterpartyUserId == null && d.CounterpartyName.ToLower() == name.ToLower(),
            ct);
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

    public async Task ApplyEntryPaymentAsync(Guid entryId, decimal amount, CancellationToken ct = default)
    {
        var entry = await db.DebtEntries.FindAsync([entryId], ct) ?? throw new InvalidOperationException();
        entry.ApplyPayment(amount);
        await db.SaveChangesAsync(ct);
    }

    public async Task ToggleEntryPaidAsync(Guid entryId, CancellationToken ct = default)
    {
        var entry = await db.DebtEntries.FindAsync([entryId], ct) ?? throw new InvalidOperationException();
        if (entry.IsPaid || entry.PaidAmount > 0m)
            entry.ClearPayment();
        else
            entry.ApplyPayment(entry.RemainingAmount);
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
