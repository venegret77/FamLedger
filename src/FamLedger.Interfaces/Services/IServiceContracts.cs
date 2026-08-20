using FamLedger.Domain.Entities;
using FamLedger.Domain.Models;

namespace FamLedger.Interfaces.Services;

public interface IBudgetPeriodService
{
    Task<BudgetPeriod> EnsureActivePeriodAsync(BudgetContext context, CancellationToken ct = default);
    Task<BudgetPeriod> ClosePeriodAsync(BudgetPeriod period, BudgetContext context, CancellationToken ct = default);
    (DateOnly Start, DateOnly End, string Label) GetPeriodBounds(BudgetContext context, DateOnly referenceDate);
}

public interface IBudgetCalculatorService
{
    Task<BudgetSummary> CalculateAsync(BudgetContext context, BudgetPeriod period, DateOnly today, CancellationToken ct = default);
}

public interface IExchangeRateService
{
    Task<decimal> ConvertToBaseAsync(decimal amount, string currency, DateOnly date, Guid contextId, Guid? periodId, CancellationToken ct = default);
    Task FetchAndStoreRatesAsync(CancellationToken ct = default);
    Task<decimal> GetRateAsync(string currency, DateOnly date, Guid contextId, Guid? periodId, CancellationToken ct = default);
}

public interface IExpenseService
{
    Task<Transaction> AddAsync(Guid contextId, Guid userId, decimal amount, string currency, Guid? categoryId, string? note, DateOnly? date, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByPeriodAsync(Guid periodId, CancellationToken ct = default);
    Task DeleteAsync(Guid transactionId, Guid userId, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetByContextAsync(Guid contextId, CancellationToken ct = default);
    Task<Category> CreateAsync(Guid contextId, string name, Guid userId, CancellationToken ct = default);
    Task UpdateAsync(Guid categoryId, string name, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid categoryId, Guid userId, CancellationToken ct = default);
    Task SeedDefaultsAsync(Guid contextId, CancellationToken ct = default);
}

public interface IRecurringExpenseService
{
    Task<RecurringExpense> CreateAsync(Guid contextId, Guid userId, string name, decimal amount, string currency, int chargeDay, CancellationToken ct = default);
    Task<IReadOnlyList<PeriodRecurringItem>> GetPeriodItemsAsync(Guid periodId, CancellationToken ct = default);
    Task TogglePaidAsync(Guid itemId, Guid userId, CancellationToken ct = default);
    Task ToggleSkippedAsync(Guid itemId, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, Guid userId, string name, decimal amount, string currency, int chargeDay, CancellationToken ct = default);
    Task AutoMarkDueItemsAsync(CancellationToken ct = default);
}

public interface IOneOffExpenseService
{
    Task<OneOffExpense> CreateAsync(Guid contextId, Guid periodId, Guid userId, string name, decimal amount, string currency, CancellationToken ct = default);
    Task<IReadOnlyList<OneOffExpense>> GetByPeriodAsync(Guid periodId, CancellationToken ct = default);
    Task TogglePaidAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}

public interface IIncomeService
{
    Task<Income> CreateAsync(Guid contextId, Guid userId, string name, decimal amount, string currency, CancellationToken ct = default);
    Task<IReadOnlyList<Income>> GetByContextAsync(Guid contextId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, Guid userId, string name, decimal amount, string currency, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}

public interface IDebtService
{
    Task<Debt> CreateAsync(Guid contextId, string counterpartyName, Guid? counterpartyUserId, Domain.Enums.DebtDirection direction, CancellationToken ct = default);
    Task<DebtEntry> AddEntryAsync(Guid debtId, decimal amount, string currency, string description, CancellationToken ct = default);
    Task ToggleEntryPaidAsync(Guid entryId, CancellationToken ct = default);
    Task DeleteAsync(Guid debtId, CancellationToken ct = default);
    Task DeleteEntryAsync(Guid entryId, CancellationToken ct = default);
    Task<IReadOnlyList<Debt>> GetByContextAsync(Guid contextId, bool hidePaid, CancellationToken ct = default);
}

public interface ISavingsService
{
    Task<SavingsEntry> GetOrCreateForPeriodAsync(Guid contextId, Guid periodId, CancellationToken ct = default);
    Task AddDepositAsync(Guid contextId, Guid periodId, decimal amount, Guid userId, CancellationToken ct = default);
    Task SetPlanAsync(Guid contextId, Guid periodId, decimal plannedAmount, Guid userId, CancellationToken ct = default);
    Task<decimal> GetTotalBalanceAsync(Guid contextId, CancellationToken ct = default);
    Task<IReadOnlyList<SavingsEntry>> GetPlansAsync(Guid contextId, CancellationToken ct = default);
}

public interface IGoalService
{
    Task<Goal> CreateAsync(Guid contextId, Guid userId, string name, decimal targetAmount, CancellationToken ct = default);
    Task ContributeAsync(Guid goalId, Guid userId, decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<Goal>> GetByContextAsync(Guid contextId, CancellationToken ct = default);
    Task DeleteAsync(Guid goalId, Guid userId, CancellationToken ct = default);
    Task CheckAndNotifyCompletedAsync(Guid goalId, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<string> AuthenticateTelegramAsync(long id, string? firstName, string? username, string? photoUrl, long authDate, string hash, CancellationToken ct = default);
    Task<string> AuthenticateByTelegramUserAsync(long telegramUserId, string? username, string? firstName, CancellationToken ct = default);
    bool ValidateTelegramHash(Dictionary<string, string> fields, string hash);
}

public interface IFileStorageService
{
    Task<string> UploadAvatarAsync(Guid userId, Stream stream, string contentType, CancellationToken ct = default);
    Task<string?> GetAvatarUrlAsync(string? avatarKey, CancellationToken ct = default);
}

public interface INotificationService
{
    Task SendTelegramAsync(long telegramUserId, string message, CancellationToken ct = default);
    Task NotifyContextMembersAsync(Guid contextId, string message, CancellationToken ct = default);
    Task SubscribeWebPushAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct = default);
    Task SendWebPushAsync(Guid userId, string title, string body, CancellationToken ct = default);
    Task DispatchWebhooksAsync(Guid userId, string eventType, object payload, CancellationToken ct = default);
}

public interface IDialogStateService
{
    Task<FamLedger.Domain.Models.DialogState?> GetAsync(long chatId, CancellationToken ct = default);
    Task SetAsync(long chatId, FamLedger.Domain.Models.DialogState state, TimeSpan? expiry, CancellationToken ct = default);
    Task ClearAsync(long chatId, CancellationToken ct = default);
}
