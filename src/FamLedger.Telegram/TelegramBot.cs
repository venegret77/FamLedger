using FamLedger.Common;
using FamLedger.Domain.Enums;
using FamLedger.Domain.Models;
using FamLedger.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FamLedger.Telegram;

public class TelegramBot(
    ITelegramBotClient botClient,
    IServiceScopeFactory scopeFactory)
{
    public Task StartReceivingAsync(CancellationToken ct)
    {
        var options = new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery] };
        botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, options, ct);
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        try
        {
            await HandleUpdateCoreAsync(client, update, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Telegram handler error: {ex.Message}");
            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
            if (chatId is not null)
            {
                await client.SendMessage(chatId.Value, "Что-то пошло не так. Попробуй ещё раз или /start login", cancellationToken: ct);
            }
        }
    }

    private async Task HandleUpdateCoreAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var userService = sp.GetRequiredService<IUserService>();
        var expenseService = sp.GetRequiredService<IExpenseService>();
        var categoryService = sp.GetRequiredService<ICategoryService>();
        var contextService = sp.GetRequiredService<IContextService>();
        var dialogState = sp.GetRequiredService<IDialogStateService>();
        var loginTokenService = sp.GetRequiredService<ILoginTokenService>();
        var calculator = sp.GetRequiredService<IBudgetCalculatorService>();
        var periodService = sp.GetRequiredService<IBudgetPeriodService>();
        var debtService = sp.GetRequiredService<IDebtService>();
        var budgetAlertService = sp.GetRequiredService<IBudgetAlertService>();

        if (update.Message is { Text: not null } msg)
        {
            var chatId = msg.Chat.Id;
            var telegramUserId = msg.From?.Id ?? chatId;
            var user = await userService.GetOrCreateByTelegramAsync(telegramUserId, msg.From?.Username, msg.From?.FirstName, ct);
            var (command, payload) = ParseIntent(msg.Text);

            if (command is "start")
            {
                if (payload.StartsWith("login", StringComparison.OrdinalIgnoreCase))
                {
                    var token = await loginTokenService.CreateAsync(telegramUserId, ct);
                    var keyboard = new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithCopyText("Скопировать код", token));
                    await client.SendMessage(chatId,
                        $"Код для входа (10 мин):\n{token}",
                        replyMarkup: keyboard,
                        cancellationToken: ct);
                    return;
                }

                await SendHelpAsync(client, chatId, user, contextService, ct);
                return;
            }

            if (command is "справка" or "help" or "меню" or "menu")
            {
                await SendHelpAsync(client, chatId, user, contextService, ct);
                return;
            }

            if (command is "статистика" or "stats")
            {
                await HandleStatsAsync(client, chatId, user, contextService, periodService, calculator, ct);
                return;
            }

            if (command is "пополнить" or "topup")
            {
                var topUpContext = await ResolveSpendContextAsync(contextService, user.Id, user.ActiveContextId, ct);
                if (topUpContext is null)
                {
                    await client.SendMessage(chatId, "Сначала войди на сайт и выбери бюджет.", cancellationToken: ct);
                    return;
                }

                if (!MoneyInputParser.TryParse(payload, out var topUpParsed, topUpContext.BaseCurrency))
                {
                    await client.SendMessage(chatId,
                        "Пример: пополнить 1000 премия или пополнить 50 eur кэшбек",
                        cancellationToken: ct);
                    return;
                }

                await BeginMoneyFlowAsync(
                    client, chatId, user, topUpParsed, "income", topUpContext,
                    categoryService, dialogState, ct);
                return;
            }

            if (command is "долг" or "debt")
            {
                var debtContext = await ResolveSpendContextAsync(contextService, user.Id, user.ActiveContextId, ct);
                if (debtContext is null)
                {
                    await client.SendMessage(chatId, "Сначала войди на сайт и выбери бюджет.", cancellationToken: ct);
                    return;
                }

                if (!MoneyInputParser.TryParse(payload, out var debtParsed, debtContext.BaseCurrency))
                {
                    await client.SendMessage(chatId,
                        "Пример: долг 1000 обед или долг 20 eur такси",
                        cancellationToken: ct);
                    return;
                }

                await BeginDebtFlowAsync(
                    client, chatId, user, debtParsed, debtContext,
                    debtService, dialogState, ct);
                return;
            }

            // waiting for new debt name?
            var existing = await dialogState.GetAsync(chatId, ct);
            if (existing?.Step == "debt_name" && existing.PendingAmount is not null)
            {
                var name = msg.Text.Trim();
                if (name.Length == 0)
                {
                    await client.SendMessage(chatId, "Введи имя человека или название.", cancellationToken: ct);
                    return;
                }

                existing.PendingDebtName = name;
                existing.Step = "debt_direction";
                await dialogState.SetAsync(chatId, existing, TimeSpan.FromHours(1), ct);

                var rows = new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Мне должны", "debt_dir:they"),
                        InlineKeyboardButton.WithCallbackData("Я должен", "debt_dir:we"),
                    }
                };
                await client.SendMessage(chatId,
                    $"«{name}» — кто кому должен?",
                    replyMarkup: new InlineKeyboardMarkup(rows),
                    cancellationToken: ct);
                return;
            }

            if (msg.Text.TrimStart().StartsWith('/'))
            {
                await client.SendMessage(chatId,
                    "Неизвестная команда. /start — справка.",
                    cancellationToken: ct);
                return;
            }

            var spendCtx = await ResolveSpendContextAsync(contextService, user.Id, user.ActiveContextId, ct);
            if (spendCtx is null)
            {
                await client.SendMessage(chatId, "Сначала войди на сайт и выбери бюджет.", cancellationToken: ct);
                return;
            }

            if (MoneyInputParser.TryParse(msg.Text, out var parsed, spendCtx.BaseCurrency))
            {
                await BeginMoneyFlowAsync(
                    client, chatId, user, parsed, "expense", spendCtx,
                    categoryService, dialogState, ct);
                return;
            }

            await client.SendMessage(chatId,
                "Не понял. Напиши сумму, пополнить / долг / статистика — или справка",
                replyMarkup: MainMenuKeyboard(),
                cancellationToken: ct);
            return;
        }

        if (update.CallbackQuery is { Data: not null } cb)
        {
            var chatId = cb.Message!.Chat.Id;
            var telegramUserId = cb.From.Id;
            var user = await userService.GetOrCreateByTelegramAsync(
                telegramUserId, cb.From.Username, cb.From.FirstName, ct);

            if (cb.Data.StartsWith("cat:"))
            {
                await HandleCategoryCallbackAsync(
                    client, cb, chatId, user, dialogState, expenseService, budgetAlertService, ct);
                return;
            }

            if (cb.Data.StartsWith("debt_pick:"))
            {
                await HandleDebtPickCallbackAsync(client, cb, chatId, user, dialogState, debtService, ct);
                return;
            }

            if (cb.Data.StartsWith("debt_dir:"))
            {
                await HandleDebtDirectionCallbackAsync(client, cb, chatId, user, dialogState, debtService, ct);
            }
        }
    }

    private static async Task SendHelpAsync(
        ITelegramBotClient client,
        long chatId,
        Domain.Entities.User user,
        IContextService contextService,
        CancellationToken ct)
    {
        var spendContext = await ResolveSpendContextAsync(contextService, user.Id, user.ActiveContextId, ct);
        var baseCur = spendContext?.BaseCurrency ?? "RSD";
        var name = user.DisplayName ?? user.FirstName ?? "друг";

        await client.SendMessage(chatId,
            $"Привет, {name}!\n\n" +
            "Списание — просто отправь сумму:\n" +
            "• 1000 кофе\n" +
            "• 10 eur хостинг\n" +
            "• 10.5 usd / €15.5 / $20\n\n" +
            $"Без валюты — {baseCur}.\n\n" +
            "Текст без слеша:\n" +
            "• пополнить 500 премия\n" +
            "• статистика\n" +
            "• долг 1000 Ивану\n" +
            "• справка / меню — эта подсказка\n\n" +
            "Для входа на сайт: /start login",
            replyMarkup: MainMenuKeyboard(),
            cancellationToken: ct);
    }

    private static ReplyKeyboardMarkup MainMenuKeyboard() =>
        new([
            ["статистика", "справка"],
            ["пополнить", "долг"],
        ])
        {
            ResizeKeyboard = true,
            IsPersistent = true,
        };

    private static async Task HandleStatsAsync(
        ITelegramBotClient client,
        long chatId,
        Domain.Entities.User user,
        IContextService contextService,
        IBudgetPeriodService periodService,
        IBudgetCalculatorService calculator,
        CancellationToken ct)
    {
        var spendContext = await ResolveSpendContextAsync(contextService, user.Id, user.ActiveContextId, ct);
        if (spendContext is null)
        {
            await client.SendMessage(chatId, "Сначала войди на сайт и выбери бюджет.", cancellationToken: ct);
            return;
        }

        var period = await periodService.EnsureActivePeriodAsync(spendContext, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await calculator.CalculateAsync(spendContext, period, today, ct);
        await client.SendMessage(chatId,
            BudgetSummaryFormatter.FormatStats(summary, spendContext.BaseCurrency, spendContext.Name),
            cancellationToken: ct);
    }

    private static async Task BeginMoneyFlowAsync(
        ITelegramBotClient client,
        long chatId,
        Domain.Entities.User user,
        ParsedMoneyInput parsed,
        string intent,
        Domain.Entities.BudgetContext spendContext,
        ICategoryService categoryService,
        IDialogStateService dialogState,
        CancellationToken ct)
    {
        var currency = parsed.Currency;
        var state = new DialogState
        {
            Step = "pick_category",
            Intent = intent,
            PendingAmount = parsed.Amount,
            PendingCurrency = currency,
            PendingNote = parsed.Remainder,
            PendingContextId = spendContext.Id
        };
        await dialogState.SetAsync(chatId, state, TimeSpan.FromHours(1), ct);

        await categoryService.SeedDefaultsAsync(spendContext.Id, ct);
        var kind = intent == "income" ? CategoryKind.Income : CategoryKind.Expense;
        var categories = (await categoryService.GetByContextAsync(spendContext.Id, ct))
            .Where(c => c.Kind == kind)
            .OrderBy(c => c.SortOrder)
            .Take(8)
            .ToList();

        var rows = categories
            .Select(c => InlineKeyboardButton.WithCallbackData(c.Name, $"cat:{c.Id}"))
            .Chunk(2)
            .Select(row => row.ToArray())
            .ToList();
        rows.Add([InlineKeyboardButton.WithCallbackData("Без категории", "cat:none")]);

        var label = intent == "income" ? "Пополнение" : "Списание";
        await client.SendMessage(chatId,
            $"{label} {MoneyFormatter.Format(parsed.Amount, currency)} в «{spendContext.Name}» — выбери категорию:",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    private static async Task BeginDebtFlowAsync(
        ITelegramBotClient client,
        long chatId,
        Domain.Entities.User user,
        ParsedMoneyInput parsed,
        Domain.Entities.BudgetContext spendContext,
        IDebtService debtService,
        IDialogStateService dialogState,
        CancellationToken ct)
    {
        var currency = parsed.Currency;
        var state = new DialogState
        {
            Step = "pick_debt",
            Intent = "debt",
            PendingAmount = parsed.Amount,
            PendingCurrency = currency,
            PendingNote = parsed.Remainder,
            PendingContextId = spendContext.Id
        };
        await dialogState.SetAsync(chatId, state, TimeSpan.FromHours(1), ct);

        var debts = await debtService.GetByContextAsync(spendContext.Id, hidePaid: false, ct);
        var rows = debts
            .Take(10)
            .Select(d =>
            {
                var dir = d.Direction == DebtDirection.TheyOwe ? "нам" : "мы";
                return InlineKeyboardButton.WithCallbackData(
                    $"{d.CounterpartyName} ({dir})",
                    $"debt_pick:{d.Id}");
            })
            .Chunk(1)
            .Select(row => row.ToArray())
            .ToList();
        rows.Add([InlineKeyboardButton.WithCallbackData("➕ Новый должник", "debt_pick:new")]);

        await client.SendMessage(chatId,
            $"Долг {MoneyFormatter.Format(parsed.Amount, currency)} — выбери кого:",
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: ct);
    }

    private static async Task HandleCategoryCallbackAsync(
        ITelegramBotClient client,
        CallbackQuery cb,
        long chatId,
        Domain.Entities.User user,
        IDialogStateService dialogState,
        IExpenseService expenseService,
        IBudgetAlertService budgetAlertService,
        CancellationToken ct)
    {
        var payload = cb.Data!["cat:".Length..];
        Guid? categoryId = payload == "none" ? null : Guid.Parse(payload);
        var state = await dialogState.GetAsync(chatId, ct);
        if (state?.PendingAmount is null || state.PendingContextId is null)
        {
            await client.AnswerCallbackQuery(cb.Id, "Сессия истекла", cancellationToken: ct);
            return;
        }

        var kind = state.Intent == "income" ? TransactionKind.Income : TransactionKind.Expense;
        var currency = state.PendingCurrency ?? "RSD";
        var contextId = state.PendingContextId.Value;
        await expenseService.AddAsync(
            contextId,
            user.Id,
            state.PendingAmount.Value,
            currency,
            categoryId,
            state.PendingNote,
            null,
            kind,
            ct);
        await dialogState.ClearAsync(chatId, ct);
        await client.AnswerCallbackQuery(cb.Id, "Записано!", cancellationToken: ct);
        var label = kind == TransactionKind.Income ? "Пополнение" : "Расход";
        await client.SendMessage(chatId,
            $"✅ {label} {MoneyFormatter.Format(state.PendingAmount.Value, currency)} записано",
            cancellationToken: ct);

        if (kind == TransactionKind.Expense)
        {
            await budgetAlertService.EvaluateAfterExpenseAsync(
                contextId, user.Id, notifyViaTelegram: true, ct);
        }
    }

    private static async Task HandleDebtPickCallbackAsync(
        ITelegramBotClient client,
        CallbackQuery cb,
        long chatId,
        Domain.Entities.User user,
        IDialogStateService dialogState,
        IDebtService debtService,
        CancellationToken ct)
    {
        var payload = cb.Data!["debt_pick:".Length..];
        var state = await dialogState.GetAsync(chatId, ct);
        if (state?.PendingAmount is null || state.PendingContextId is null || state.Intent != "debt")
        {
            await client.AnswerCallbackQuery(cb.Id, "Сессия истекла", cancellationToken: ct);
            return;
        }

        if (payload == "new")
        {
            state.Step = "debt_name";
            await dialogState.SetAsync(chatId, state, TimeSpan.FromHours(1), ct);
            await client.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
            await client.SendMessage(chatId, "Как зовут? Напиши имя:", cancellationToken: ct);
            return;
        }

        var debtId = Guid.Parse(payload);
        var currency = state.PendingCurrency ?? "RSD";
        await debtService.AddEntryAsync(
            debtId,
            state.PendingAmount.Value,
            currency,
            state.PendingNote ?? string.Empty,
            ct);
        await dialogState.ClearAsync(chatId, ct);
        await client.AnswerCallbackQuery(cb.Id, "Записано!", cancellationToken: ct);
        await client.SendMessage(chatId,
            $"✅ Долг {MoneyFormatter.Format(state.PendingAmount.Value, currency)} записан",
            cancellationToken: ct);
    }

    private static async Task HandleDebtDirectionCallbackAsync(
        ITelegramBotClient client,
        CallbackQuery cb,
        long chatId,
        Domain.Entities.User user,
        IDialogStateService dialogState,
        IDebtService debtService,
        CancellationToken ct)
    {
        var payload = cb.Data!["debt_dir:".Length..];
        var state = await dialogState.GetAsync(chatId, ct);
        if (state?.PendingAmount is null
            || state.PendingContextId is null
            || string.IsNullOrWhiteSpace(state.PendingDebtName))
        {
            await client.AnswerCallbackQuery(cb.Id, "Сессия истекла", cancellationToken: ct);
            return;
        }

        var direction = payload == "they" ? DebtDirection.TheyOwe : DebtDirection.WeOwe;
        var debt = await debtService.CreateAsync(
            state.PendingContextId.Value,
            state.PendingDebtName,
            null,
            direction,
            ct);
        var currency = state.PendingCurrency ?? "RSD";
        await debtService.AddEntryAsync(
            debt.Id,
            state.PendingAmount.Value,
            currency,
            state.PendingNote ?? string.Empty,
            ct);
        await dialogState.ClearAsync(chatId, ct);
        await client.AnswerCallbackQuery(cb.Id, "Записано!", cancellationToken: ct);
        await client.SendMessage(chatId,
            $"✅ Долг {MoneyFormatter.Format(state.PendingAmount.Value, currency)} на «{state.PendingDebtName}» записан",
            cancellationToken: ct);
    }

    private static async Task<Domain.Entities.BudgetContext?> ResolveSpendContextAsync(
        IContextService contextService,
        Guid userId,
        Guid? activeContextId,
        CancellationToken ct)
    {
        var contexts = await contextService.GetUserContextsAsync(userId, ct);
        if (contexts.Count == 0) return null;

        var family = contexts.FirstOrDefault(c => !c.IsPersonal);
        if (family is not null) return family;

        if (activeContextId is Guid activeId)
        {
            var active = contexts.FirstOrDefault(c => c.Id == activeId);
            if (active is not null) return active;
        }

        return contexts[0];
    }

    private static Task HandleErrorAsync(ITelegramBotClient client, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"Telegram error: {ex.Message}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// First word as intent (optional leading /), rest as payload.
    /// Examples: "пополнить 500", "/start login", "статистика".
    /// </summary>
    private static (string Command, string Payload) ParseIntent(string text)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0];
        var at = command.IndexOf('@');
        if (at >= 0) command = command[..at];
        if (command.StartsWith('/'))
            command = command[1..];
        var payload = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return (command.ToLowerInvariant(), payload);
    }
}
