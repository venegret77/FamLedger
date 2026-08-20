using FamLedger.Common;
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
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var expenseService = scope.ServiceProvider.GetRequiredService<IExpenseService>();
        var categoryService = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var dialogState = scope.ServiceProvider.GetRequiredService<IDialogStateService>();
        var loginTokenService = scope.ServiceProvider.GetRequiredService<ILoginTokenService>();

        if (update.Message is { Text: not null } msg)
        {
            var chatId = msg.Chat.Id;
            var user = await userService.GetOrCreateByTelegramAsync(chatId, msg.From?.Username, msg.From?.FirstName, ct);

            if (ParseCommand(msg.Text) is ("/start", var payload))
            {
                if (payload.StartsWith("login", StringComparison.OrdinalIgnoreCase))
                {
                    var token = await loginTokenService.CreateAsync(chatId, ct);
                    await client.SendMessage(chatId,
                        $"Код для входа (10 мин):\n`{token}`",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct);
                    return;
                }

                await client.SendMessage(chatId,
                    $"Привет, {user.DisplayName ?? user.FirstName}! Отправь сумму расхода, например: 1000\n\nДля входа на сайт: /start login",
                    cancellationToken: ct);
                return;
            }

            if (MoneyInputParser.TryParse(msg.Text, out var parsed))
            {
                if (user.ActiveContextId is null)
                {
                    await client.SendMessage(chatId, "Сначала войди на сайт и выбери бюджет.", cancellationToken: ct);
                    return;
                }

                var state = new DialogState
                {
                    Step = "pick_category",
                    PendingAmount = parsed.Amount,
                    PendingCurrency = parsed.Currency,
                    PendingContextId = user.ActiveContextId
                };
                await dialogState.SetAsync(chatId, state, TimeSpan.FromHours(1), ct);

                var categories = await categoryService.GetByContextAsync(user.ActiveContextId.Value, ct);
                var buttons = categories.Take(8).Select(c =>
                    InlineKeyboardButton.WithCallbackData(c.Name, $"cat:{c.Id}")).Chunk(2)
                    .Select(row => row.ToArray()).ToArray();

                await client.SendMessage(chatId,
                    $"Записать {MoneyFormatter.Format(parsed.Amount, parsed.Currency)} — выбери категорию:",
                    replyMarkup: new InlineKeyboardMarkup(buttons),
                    cancellationToken: ct);
                return;
            }

            await client.SendMessage(chatId, "Отправь сумму расхода или /start", cancellationToken: ct);
        }

        if (update.CallbackQuery is { Data: not null } cb)
        {
            var chatId = cb.Message!.Chat.Id;
            if (cb.Data.StartsWith("cat:"))
            {
                var categoryId = Guid.Parse(cb.Data["cat:".Length..]);
                var state = await dialogState.GetAsync(chatId, ct);
                if (state?.PendingAmount is null || state.PendingContextId is null)
                {
                    await client.AnswerCallbackQuery(cb.Id, "Сессия истекла", cancellationToken: ct);
                    return;
                }

                var user = await userService.GetOrCreateByTelegramAsync(chatId, cb.From.Username, cb.From.FirstName, ct);
                await expenseService.AddAsync(state.PendingContextId.Value, user.Id,
                    state.PendingAmount.Value, state.PendingCurrency ?? "RSD", categoryId, null, null, ct);
                await dialogState.ClearAsync(chatId, ct);
                await client.AnswerCallbackQuery(cb.Id, "Записано!", cancellationToken: ct);
                await client.SendMessage(chatId, $"✅ Расход {MoneyFormatter.Format(state.PendingAmount.Value, state.PendingCurrency ?? "RSD")} записан", cancellationToken: ct);
            }
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient client, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"Telegram error: {ex.Message}");
        return Task.CompletedTask;
    }

    private static (string Command, string Payload) ParseCommand(string text)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0];
        var at = command.IndexOf('@');
        if (at >= 0) command = command[..at];
        var payload = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return (command, payload);
    }
}
