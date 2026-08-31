using FamLedger.Domain.Models;

namespace FamLedger.Interfaces.Services;

public interface IReconciliationService
{
    Task<ReconciliationView> GetAsync(Guid contextId, Guid periodId, Guid userId, CancellationToken ct = default);
    Task<ReconciliationView> SaveManualAsync(
        Guid contextId,
        Guid periodId,
        Guid userId,
        ReconciliationManualInput manual,
        CancellationToken ct = default);
}
