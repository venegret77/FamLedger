using FamLedger.Domain.Entities;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Services;

public class DebtService(AppDbContext db) : IDebtService
{
    public async Task<Debt> CreateAsync(Guid contextId, Guid userId, string counterpartyName, Guid? counterpartyUserId, DebtDirection direction, CancellationToken ct = default)
    {
        if (!await db.ContextMembers.AnyAsync(member => member.ContextId == contextId && member.UserId == userId, ct))
            throw new UnauthorizedAccessException();

        if (counterpartyUserId is { } counterparty
            && !await db.ContextMembers.AnyAsync(member => member.ContextId == contextId && member.UserId == counterparty, ct))
            throw new ArgumentException("Контрагент не входит в бюджет.");

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

    public async Task<DebtEntry> AddEntryAsync(Guid contextId, Guid userId, Guid debtId, decimal amount, string currency, string description, CancellationToken ct = default)
    {
        if (!await AccessibleDebts(contextId, userId).AnyAsync(debt => debt.Id == debtId, ct))
            throw new KeyNotFoundException();
        if (amount <= 0)
            throw new ArgumentException("Сумма должна быть больше нуля.");

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

    public async Task ApplyEntryPaymentAsync(Guid contextId, Guid userId, Guid entryId, decimal amount, CancellationToken ct = default)
    {
        var entry = await GetAccessibleEntryAsync(contextId, userId, entryId, ct);
        entry.ApplyPayment(amount);
        await db.SaveChangesAsync(ct);
    }

    public async Task ToggleEntryPaidAsync(Guid contextId, Guid userId, Guid entryId, CancellationToken ct = default)
    {
        var entry = await GetAccessibleEntryAsync(contextId, userId, entryId, ct);
        if (entry.IsPaid || entry.PaidAmount > 0m)
            entry.ClearPayment();
        else
            entry.ApplyPayment(entry.RemainingAmount);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid contextId, Guid userId, Guid debtId, CancellationToken ct = default)
    {
        var deleted = await AccessibleDebts(contextId, userId).Where(debt => debt.Id == debtId).ExecuteDeleteAsync(ct);
        if (deleted == 0)
            throw new KeyNotFoundException();
    }

    public async Task DeleteEntryAsync(Guid contextId, Guid userId, Guid entryId, CancellationToken ct = default)
    {
        var entry = await GetAccessibleEntryAsync(contextId, userId, entryId, ct);
        db.DebtEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Debt> AccessibleDebts(Guid contextId, Guid userId) =>
        db.Debts.Where(debt => debt.ContextId == contextId && debt.Context.Members.Any(member => member.UserId == userId));

    private async Task<DebtEntry> GetAccessibleEntryAsync(Guid contextId, Guid userId, Guid entryId, CancellationToken ct)
    {
        var debts = AccessibleDebts(contextId, userId);
        return await db.DebtEntries.SingleOrDefaultAsync(entry => entry.Id == entryId && debts.Any(debt => debt.Id == entry.DebtId), ct)
            ?? throw new KeyNotFoundException();
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
