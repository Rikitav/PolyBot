using PolyBot;
using PolyBot.Attributes;
using PolyBot.Commands;
using PolyBot.Routing;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CommandBot;

/// <summary>
/// Demonstrates command routing, argument parsing, custom prefixes, and keyboards.
/// </summary>
public sealed partial class BotHandlers
{
    // Generated keyboard: two buttons in one row.
    [CallbackButton("Help", "help"), CallbackButton("About", "about")]
    private static partial InlineKeyboardMarkup MainMenuKeyboard();

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["start"], Description = "Start the bot")]
    public static async Task<Result> Start(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(
            msg.Chat.Id,
            "Welcome to CommandBot!\nTry: /add 5 3, /greet Bob, /say 2 hello world, /menu",
            replyMarkup: MainMenuKeyboard(),
            cancellationToken: ct);

        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["add"], Description = "Add two integers")]
    public static async Task<Result> Add(
        Message msg,
        ITelegramBotClient bot,
        [Arg] int a,
        [Arg] int b,
        CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"{a} + {b} = {a + b}", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["greet"], Description = "Greet someone")]
    public static async Task<Result> Greet(
        Message msg,
        ITelegramBotClient bot,
        [Arg(IsOptional = true)] string? name,
        CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"Hello, {name ?? "stranger"}!", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["say"], Description = "Repeat a phrase N times")]
    public static async Task<Result> Say(
        Message msg,
        ITelegramBotClient bot,
        [Arg] int times,
        [Rest] string phrase,
        CancellationToken ct)
    {
        string repeated = string.Join(" ", Enumerable.Repeat(phrase, times));
        await bot.SendMessage(msg.Chat.Id, repeated, cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["menu"], Description = "Show the inline menu")]
    public static async Task<Result> Menu(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "Pick an option:", replyMarkup: MainMenuKeyboard(), cancellationToken: ct);
        return Result.Handled();
    }

    // Custom prefix: matches messages starting with "!help".
    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["help"], Prefix = '!', Description = "Show help (use !help)")]
    public static async Task<Result> Help(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(
            msg.Chat.Id,
            "Commands:\n" +
            "/add a b – add two numbers\n" +
            "/greet [name] – greet someone\n" +
            "/say N phrase – repeat a phrase\n" +
            "/menu – show the menu\n" +
            "!help – this message",
            cancellationToken: ct);

        return Result.Handled();
    }

    [CallbackQueryHandler(Priority = -1)]
    [Pattern("help")]
    public static async Task<Result> OnHelpCallback(CallbackQuery query, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.AnswerCallbackQuery(query.Id, "Use /start or /menu.", cancellationToken: ct);
        return Result.Handled();
    }

    [CallbackQueryHandler(Priority = -1)]
    [Pattern("about")]
    public static async Task<Result> OnAboutCallback(CallbackQuery query, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.AnswerCallbackQuery(query.Id, "CommandBot built with PolyBot.", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -100)]
    public static async Task<Result> Echo(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"echo: {msg.Text}", cancellationToken: ct);
        return Result.Handled();
    }

    [ExceptionHandler]
    public static async ValueTask OnError(Exception exc, ITelegramBotClient bot, CancellationToken ct)
    {
        if (exc is CommandArgsParseException argParseExc)
        {
            if (argParseExc.SourceMessage?.Text is not null)
                await bot.SendMessage(argParseExc.SourceMessage.Chat, argParseExc.Message, cancellationToken: ct);

            return;
        }
    }
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
