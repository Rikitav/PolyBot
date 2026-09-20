using PolyBot;
using PolyBot.Attributes;
using PolyBot.Awaits;
using PolyBot.Routing;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConversationBot;

/// <summary>
/// Demonstrates conversations with <see cref="IUpdateAwaiter"/>, callback-data patterns,
/// rate limiting, and a global exception handler.
/// </summary>
public sealed partial class BotHandlers
{
    // Generated confirmation keyboard: {userId} is interpolated into the callback data.
    [CallbackButton("Confirm", "confirm:{userId}"), CallbackButton("Cancel", "cancel:{userId}")]
    private static partial InlineKeyboardMarkup ConfirmKeyboard(long userId);

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["start"], Description = "Start the bot")]
    public static async Task<Result> Start(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(
            msg.Chat.Id,
            "Welcome to ConversationBot!\nSend /register to start a short interview.",
            cancellationToken: ct);

        return Result.Handled();
    }

    /// <summary>
    /// Starts a registration interview. The handler suspends twice to wait for user replies,
    /// so the polling loop keeps processing other updates while the conversation is pending.
    /// </summary>
    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["register"], Description = "Start registration interview")]
    [Throttled(Limit = 1, PeriodMilliseconds = 10_000, Scope = ThrottleScope.User, Action = ThrottleAction.Fallthrough)]
    public static async Task<Result> Register(
        Message msg,
        IUpdateAwaiter awaiter,
        ITelegramBotClient bot,
        CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "What is your name?", cancellationToken: ct);

        Message? nameMessage = await awaiter.WaitForMessageAsync()
            .WithNonEmptyTextFilter()
            .ByUserId(ct);

        if (nameMessage is null)
        {
            await bot.SendMessage(msg.Chat.Id, "Registration timed out.", cancellationToken: ct);
            return Result.Handled();
        }

        string name = nameMessage.Text!;

        await bot.SendMessage(msg.Chat.Id, $"Hi {name}. How old are you?", cancellationToken: ct);

        Message? ageMessage = await awaiter.WaitForMessageAsync()
            .TextMatches(@"^\d+$")
            .ByUserId(ct);

        if (ageMessage is null)
        {
            await bot.SendMessage(msg.Chat.Id, "Registration timed out.", cancellationToken: ct);
            return Result.Handled();
        }

        int age = int.Parse(ageMessage.Text!, System.Globalization.CultureInfo.InvariantCulture);
        long userId = msg.From!.Id;

        await bot.SendMessage(
            msg.Chat.Id,
            $"{name}, {age} years old — please confirm:",
            replyMarkup: ConfirmKeyboard(userId),
            cancellationToken: ct);

        return Result.Handled();
    }

    [CallbackQueryHandler(Priority = -1)]
    [Pattern("confirm:{userId}")]
    public static async Task<Result> OnConfirm(
        CallbackQuery query,
        ITelegramBotClient bot,
        [Arg] long userId,
        CancellationToken ct)
    {
        if (query.From.Id != userId)
        {
            await bot.AnswerCallbackQuery(query.Id, "This button is not for you.", cancellationToken: ct);
            return Result.Handled();
        }

        await bot.AnswerCallbackQuery(query.Id, "Confirmed!", cancellationToken: ct);
        await bot.SendMessage(query.Message!.Chat.Id, "Registration complete.", cancellationToken: ct);
        return Result.Handled();
    }

    [CallbackQueryHandler(Priority = -1)]
    [Pattern("cancel:{userId}")]
    public static async Task<Result> OnCancel(
        CallbackQuery query,
        ITelegramBotClient bot,
        [Arg] long userId,
        CancellationToken ct)
    {
        if (query.From.Id != userId)
        {
            await bot.AnswerCallbackQuery(query.Id, "This button is not for you.", cancellationToken: ct);
            return Result.Handled();
        }

        await bot.AnswerCallbackQuery(query.Id, "Cancelled.", cancellationToken: ct);
        await bot.SendMessage(query.Message!.Chat.Id, "Registration cancelled.", cancellationToken: ct);
        return Result.Handled();
    }

    [ExceptionHandler]
    public static Task OnError(Exception exception, HandleErrorSource source)
    {
        Debug.WriteLine($"[{source}] {exception.GetType().Name}: {exception.Message}");
        return Task.CompletedTask;
    }
}

// A simple non-empty text filter used by the conversation await chain.
public sealed class NonEmptyTextFilter : MessageFilter
{
    protected override bool CanPass(Message message) => !string.IsNullOrEmpty(message.Text);
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        string? token = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                "No bot token provided: pass it as the first argument or set the TELEGRAM_BOT_TOKEN environment variable.");
        }

        await using PolyBotClient client = new PolyBotClient(new PolyBotOptions() { BotToken = token });
        client.Services.AddPolyBotRouter();

        await client.RunPollingAsync();
    }
}
