using Microsoft.Extensions.Configuration;
using PolyBot;
using PolyBot.Attributes;
using PolyBot.Routing;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EchoBot;

public sealed partial class BotHandlers
{
    [CallbackButton("Help", "help"), CallbackButton("About", "about")]
    private static partial InlineKeyboardMarkup StartKeyboard();

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["start"], Description = "Start the bot")]
    public static async Task<Result> Start(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "Welcome! I echo everything you say.", replyMarkup: StartKeyboard(), cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = 0)]
    public static async Task<Result> Echo(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, msg.Text ?? string.Empty, cancellationToken: ct);
        return Result.Handled();
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

        PolyBotClient client = new PolyBotClient(new PolyBotOptions() { BotToken = token });
        client.Services.AddPolyBotRouter();

        await client.RunPollingAsync();
    }
}
