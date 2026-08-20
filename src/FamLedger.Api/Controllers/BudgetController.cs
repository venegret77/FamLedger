using FamLedger.Api.Extensions;
using FamLedger.Interfaces.Services;
using FamLedger.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamLedger.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class BudgetController(
    AppDbContext db,
    IUserService userService,
    IBudgetPeriodService periodService,
    IBudgetCalculatorService calculator,
    IExpenseService expenseService,
    ICategoryService categoryService,
    IRecurringExpenseService recurringService,
    IOneOffExpenseService oneOffService,
    IIncomeService incomeService,
    IDebtService debtService,
    ISavingsService savingsService,
    IGoalService goalService,
    IExchangeRateService exchangeRateService) : ControllerBase
{
    private async Task<(Domain.Entities.BudgetContext Context, Domain.Entities.BudgetPeriod Period)> GetActiveContextAsync(CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(User.GetUserId(), ct)
            ?? throw new InvalidOperationException("User not found");
        if (user.ActiveContextId is null) throw new InvalidOperationException("No active context");
        var context = await db.BudgetContexts.FindAsync([user.ActiveContextId.Value], ct)
            ?? throw new InvalidOperationException("Context not found");
        var period = await periodService.EnsureActivePeriodAsync(context, ct);
        return (context, period);
    }

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var (context, period) = await GetActiveContextAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await calculator.CalculateAsync(context, period, today, ct);
        return Ok(new
        {
            summary.Income,
            summary.PlannedExpenses,
            summary.Spent,
            summary.Carryover,
            summary.Remaining,
            summary.DailyBudgetAtStart,
            summary.DailyBudgetNow,
            summary.AvailableToday,
            summary.SpentToday,
            summary.DaysInPeriod,
            summary.DaysPassed,
            summary.DaysRemaining,
            summary.PeriodLabel,
            summary.PeriodId,
            currency = context.BaseCurrency
        });
    }

    public record AddTransactionRequest(decimal Amount, string Currency, Guid? CategoryId, string? Note, DateOnly? Date);

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions(CancellationToken ct)
    {
        var (_, period) = await GetActiveContextAsync(ct);
        var list = await expenseService.GetByPeriodAsync(period.Id, ct);
        return Ok(list.Select(t => new
        {
            t.Id,
            t.Amount,
            t.Currency,
            t.BaseAmount,
            t.Date,
            t.Note,
            categoryName = t.Category?.Name,
            createdByName = t.CreatedByUser.DisplayName ?? t.CreatedByUser.FirstName,
            t.CreatedAt
        }));
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> AddTransaction([FromBody] AddTransactionRequest request, CancellationToken ct)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        var tx = await expenseService.AddAsync(context.Id, User.GetUserId(), request.Amount, request.Currency, request.CategoryId, request.Note, request.Date, ct);
        return Ok(new { tx.Id });
    }

    [HttpDelete("transactions/{id:guid}")]
    public async Task<IActionResult> DeleteTransaction(Guid id, CancellationToken ct)
    {
        await expenseService.DeleteAsync(id, User.GetUserId(), ct);
        return Ok();
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        var cats = await categoryService.GetByContextAsync(context.Id, ct);
        return Ok(cats.Select(c => new { c.Id, c.Name, c.Kind, c.IsDefault }));
    }

    public record CategoryRequest(string Name);

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest request, CancellationToken ct)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        var cat = await categoryService.CreateAsync(context.Id, request.Name, User.GetUserId(), ct);
        return Ok(new { cat.Id, cat.Name });
    }

    [HttpPatch("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryRequest request, CancellationToken ct)
    {
        await categoryService.UpdateAsync(id, request.Name, User.GetUserId(), ct);
        return Ok();
    }

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        await categoryService.DeleteAsync(id, User.GetUserId(), ct);
        return Ok();
    }

    [HttpGet("plan/recurring")]
    public async Task<IActionResult> Recurring(CancellationToken ct)
    {
        var (_, period) = await GetActiveContextAsync(ct);
        var items = await recurringService.GetPeriodItemsAsync(period.Id, ct);
        return Ok(items.Select(i => new
        {
            i.Id,
            recurringExpenseId = i.RecurringExpenseId,
            i.IsPaid,
            i.IsSkipped,
            periodAmount = i.PlannedBaseAmount,
            i.PlannedBaseAmount,
            i.PaidAt,
            name = i.RecurringExpense.Name,
            chargeDayOfMonth = i.RecurringExpense.ChargeDayOfMonth,
            definitionAmount = i.RecurringExpense.DefinitionAmount,
            definitionCurrency = i.RecurringExpense.DefinitionCurrency
        }));
    }

    public record RecurringRequest(string Name, decimal Amount, string Currency, int ChargeDay);

    [HttpPost("plan/recurring")]
    public async Task<IActionResult> CreateRecurring([FromBody] RecurringRequest request, CancellationToken ct)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        var item = await recurringService.CreateAsync(context.Id, User.GetUserId(), request.Name, request.Amount, request.Currency, request.ChargeDay, ct);
        return Ok(new { item.Id });
    }

    [HttpPatch("plan/recurring/expenses/{recurringExpenseId:guid}")]
    public async Task<IActionResult> UpdateRecurring(Guid recurringExpenseId, [FromBody] RecurringRequest request, CancellationToken ct)
    {
        await recurringService.UpdateAsync(recurringExpenseId, User.GetUserId(), request.Name, request.Amount, request.Currency, request.ChargeDay, ct);
        return Ok();
    }

    [HttpPatch("plan/recurring/{id:guid}/toggle-paid")]
    public async Task<IActionResult> ToggleRecurringPaid(Guid id, CancellationToken ct)
    {
        await recurringService.TogglePaidAsync(id, User.GetUserId(), ct);
        return Ok();
    }

    [HttpPatch("plan/recurring/{id:guid}/toggle-skip")]
    public async Task<IActionResult> ToggleRecurringSkip(Guid id, CancellationToken ct)
    {
        await recurringService.ToggleSkippedAsync(id, User.GetUserId(), ct);
        return Ok();
    }

    [HttpDelete("plan/recurring/expenses/{recurringExpenseId:guid}")]
    public async Task<IActionResult> DeleteRecurring(Guid recurringExpenseId, CancellationToken ct)
    {
        await recurringService.DeleteAsync(recurringExpenseId, User.GetUserId(), ct);
        return Ok();
    }

    [HttpGet("plan/one-off")]
    public async Task<IActionResult> OneOff(CancellationToken ct)
    {
        var (_, period) = await GetActiveContextAsync(ct);
        var items = await oneOffService.GetByPeriodAsync(period.Id, ct);
        return Ok(items.Select(i => new { i.Id, i.Name, i.Amount, i.Currency, i.BaseAmount, i.IsPaid }));
    }

    public record OneOffRequest(string Name, decimal Amount, string Currency);

    [HttpPost("plan/one-off")]
    public async Task<IActionResult> CreateOneOff([FromBody] OneOffRequest request, CancellationToken ct)
    {
        var (context, period) = await GetActiveContextAsync(ct);
        var item = await oneOffService.CreateAsync(context.Id, period.Id, User.GetUserId(), request.Name, request.Amount, request.Currency, ct);
        return Ok(new { item.Id });
    }

    [HttpPatch("plan/one-off/{id:guid}/toggle-paid")]
    public async Task<IActionResult> ToggleOneOffPaid(Guid id, CancellationToken ct)
    {
        await oneOffService.TogglePaidAsync(id, User.GetUserId(), ct);
        return Ok();
    }

    [HttpDelete("plan/one-off/{id:guid}")]
    public async Task<IActionResult> DeleteOneOff(Guid id, CancellationToken ct)
    {
        await oneOffService.DeleteAsync(id, User.GetUserId(), ct);
        return Ok();
    }

    [HttpGet("plan/incomes")]
    public async Task<IActionResult> Incomes(CancellationToken ct)
    {
        var (context, period) = await GetActiveContextAsync(ct);
        var items = await incomeService.GetByContextAsync(context.Id, ct);
        var result = new List<object>();
        foreach (var i in items)
        {
            var baseAmount = i.Currency.Equals(context.BaseCurrency, StringComparison.OrdinalIgnoreCase)
                ? i.Amount
                : await exchangeRateService.ConvertToBaseAsync(
                    i.Amount, i.Currency, period.StartDate, context.Id, period.Id, ct);
            result.Add(new { i.Id, i.Name, i.Amount, i.Currency, i.SortOrder, baseAmount });
        }
        return Ok(result);
    }

    public record IncomeRequest(string Name, decimal Amount, string Currency);

    [HttpPost("plan/incomes")]
    public async Task<IActionResult> CreateIncome([FromBody] IncomeRequest request, CancellationToken ct)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        var item = await incomeService.CreateAsync(context.Id, User.GetUserId(), request.Name, request.Amount, request.Currency, ct);
        return Ok(new { item.Id });
    }

    [HttpPatch("plan/incomes/{id:guid}")]
    public async Task<IActionResult> UpdateIncome(Guid id, [FromBody] IncomeRequest request, CancellationToken ct)
    {
        await incomeService.UpdateAsync(id, User.GetUserId(), request.Name, request.Amount, request.Currency, ct);
        return Ok();
    }

    [HttpDelete("plan/incomes/{id:guid}")]
    public async Task<IActionResult> DeleteIncome(Guid id, CancellationToken ct)
    {
        await incomeService.DeleteAsync(id, User.GetUserId(), ct);
        return Ok();
    }

    [HttpGet("debts")]
    public async Task<IActionResult> Debts([FromQuery] bool hidePaid = false, CancellationToken ct = default)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        var debts = await debtService.GetByContextAsync(context.Id, hidePaid, ct);
        return Ok(debts.Select(d =>
        {
            var openEntries = d.Entries.Where(e => !e.IsPaid).ToList();
            var balance = openEntries.Sum(e => e.Amount);
            var currency = openEntries.FirstOrDefault()?.Currency ?? context.BaseCurrency;
            var direction = d.Direction == Domain.Enums.DebtDirection.TheyOwe ? "OwedToUs" : "WeOwe";
            return new
            {
                d.Id,
                d.CounterpartyName,
                counterpartyUserId = d.CounterpartyUserId,
                direction,
                balance,
                currency,
                entries = d.Entries.OrderByDescending(e => e.CreatedAt).Select(e => new
                {
                    e.Id,
                    e.Amount,
                    e.Currency,
                    e.Description,
                    e.IsPaid,
                    e.CreatedAt
                })
            };
        }));
    }

    public record DebtRequest(string CounterpartyName, Guid? CounterpartyUserId, Domain.Enums.DebtDirection Direction);
    public record DebtEntryRequest(decimal Amount, string Currency, string Description);

    [HttpPost("debts")]
    public async Task<IActionResult> CreateDebt([FromBody] DebtRequest request, CancellationToken ct)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        var debt = await debtService.CreateAsync(context.Id, request.CounterpartyName, request.CounterpartyUserId, request.Direction, ct);
        return Ok(new { debt.Id });
    }

    [HttpPost("debts/{debtId:guid}/entries")]
    public async Task<IActionResult> AddDebtEntry(Guid debtId, [FromBody] DebtEntryRequest request, CancellationToken ct)
    {
        var entry = await debtService.AddEntryAsync(debtId, request.Amount, request.Currency, request.Description, ct);
        return Ok(new { entry.Id });
    }

    [HttpPatch("debts/entries/{entryId:guid}/toggle-paid")]
    public async Task<IActionResult> ToggleDebtPaid(Guid entryId, CancellationToken ct)
    {
        await debtService.ToggleEntryPaidAsync(entryId, ct);
        return Ok();
    }

    [HttpDelete("debts/{debtId:guid}")]
    public async Task<IActionResult> DeleteDebt(Guid debtId, CancellationToken ct)
    {
        await debtService.DeleteAsync(debtId, ct);
        return Ok();
    }

    [HttpDelete("debts/entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteDebtEntry(Guid entryId, CancellationToken ct)
    {
        await debtService.DeleteEntryAsync(entryId, ct);
        return Ok();
    }

    [HttpGet("savings")]
    public async Task<IActionResult> Savings(CancellationToken ct)
    {
        var (context, period) = await GetActiveContextAsync(ct);
        var balance = await savingsService.GetTotalBalanceAsync(context.Id, ct);
        var entry = await savingsService.GetOrCreateForPeriodAsync(context.Id, period.Id, ct);
        var plans = await savingsService.GetPlansAsync(context.Id, ct);
        var goals = await goalService.GetByContextAsync(context.Id, ct);
        return Ok(new
        {
            balance,
            current = new { entry.PlannedAmount, entry.ActualAmount },
            plans = plans.Select(p => new
            {
                p.Id,
                p.PlannedAmount,
                p.ActualAmount,
                p.Currency,
                PeriodLabel = p.Period?.Label,
                PeriodStart = p.Period?.StartDate,
                PeriodEnd = p.Period?.EndDate
            }),
            goals = goals.Select(g => new
            {
                g.Id,
                g.Name,
                g.TargetAmount,
                g.Currency,
                g.IsCompleted,
                Progress = g.Contributions.Sum(c => c.Amount)
            })
        });
    }

    public record SavingsDepositRequest(decimal Amount, string? Currency);
    public record SavingsPlanRequest(decimal PlannedAmount, string? Currency);
    public record GoalRequest(string Name, decimal TargetAmount, string? Currency);
    public record GoalContributeRequest(decimal Amount, string? Currency);

    [HttpPost("savings/deposit")]
    public async Task<IActionResult> Deposit([FromBody] SavingsDepositRequest request, CancellationToken ct)
    {
        var (context, period) = await GetActiveContextAsync(ct);
        await savingsService.AddDepositAsync(
            context.Id,
            period.Id,
            request.Amount,
            request.Currency ?? context.BaseCurrency,
            User.GetUserId(),
            ct);
        return Ok();
    }

    [HttpPost("savings/plan")]
    public async Task<IActionResult> SetPlan([FromBody] SavingsPlanRequest request, CancellationToken ct)
    {
        var (context, period) = await GetActiveContextAsync(ct);
        await savingsService.SetPlanAsync(
            context.Id,
            period.Id,
            request.PlannedAmount,
            request.Currency ?? context.BaseCurrency,
            User.GetUserId(),
            ct);
        return Ok();
    }

    [HttpPost("savings/goals")]
    public async Task<IActionResult> CreateGoal([FromBody] GoalRequest request, CancellationToken ct)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        var goal = await goalService.CreateAsync(
            context.Id,
            User.GetUserId(),
            request.Name,
            request.TargetAmount,
            request.Currency ?? context.BaseCurrency,
            ct);
        return Ok(new { goal.Id });
    }

    [HttpPost("savings/goals/{goalId:guid}/contribute")]
    public async Task<IActionResult> ContributeGoal(Guid goalId, [FromBody] GoalContributeRequest request, CancellationToken ct)
    {
        var (context, _) = await GetActiveContextAsync(ct);
        await goalService.ContributeAsync(
            goalId,
            User.GetUserId(),
            request.Amount,
            request.Currency ?? context.BaseCurrency,
            ct);
        return Ok();
    }

    [HttpDelete("savings/goals/{goalId:guid}")]
    public async Task<IActionResult> DeleteGoal(Guid goalId, CancellationToken ct)
    {
        await goalService.DeleteAsync(goalId, User.GetUserId(), ct);
        return Ok();
    }
}
