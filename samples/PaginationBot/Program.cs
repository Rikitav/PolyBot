using PolyBot;
using PolyBot.Attributes;
using PolyBot.Awaits;
using PolyBot.Keyboards;
using PolyBot.Routing;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace PaginationBot;

/// <summary>
/// Demonstrates paginated inline keyboards combined with inline awaits.
/// The handler suspends and resumes on each page navigation click, so the
/// polling loop stays free for other users while the paginator is open.
/// </summary>
public sealed partial class BotHandlers
{
    private static readonly string[] Items =
    [
        "Apple", "Banana", "Cherry", "Date", "Elderberry",
        "Fig", "Grape", "Honeydew", "Kiwi", "Lemon",
        "Mango", "Nectarine", "Orange", "Papaya", "Quince"
    ];

    private const int PageSize = 3;
    private static readonly TimeSpan PaginationTimeout = TimeSpan.FromMinutes(2);

    [MessageHandler]
    [Command(Aliases = ["items"], Description = "Show a paginated item list")]
    public static async Task<Result> ShowItems(
        Message msg,
        IUpdateAwaiter awaiter,
        ITelegramBotClient bot,
        CancellationToken ct)
    {
        int page = 0;
        int maxPage = (Items.Length - 1) / PageSize;

        Message listMessage = await bot.SendMessage(
            msg.Chat.Id,
            FormatPageText(page, maxPage),
            replyMarkup: BuildKeyboardPage(page, maxPage),
            cancellationToken: ct);

        while (true)
        {
            CallbackQuery? query = await awaiter.WaitForCallbackQueryAsync(PaginationTimeout)
                .ByUserId(ct);

            if (query is null)
            {
                await bot.EditMessageText(
                    listMessage.Chat.Id,
                    listMessage.MessageId,
                    "Pagination timed out.",
                    cancellationToken: ct);

                return Result.Handled();
            }

            await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);

            string data = query.Data ?? string.Empty;
            if (data.StartsWith("item:", StringComparison.Ordinal))
            {
                string item = data.Substring("item:".Length);
                await bot.SendMessage(
                    listMessage.Chat.Id,
                    $"You selected: {item}",
                    cancellationToken: ct);

                return Result.Handled();
            }

            switch (data)
            {
                case "done":
                    {
                        await bot.EditMessageText(
                            listMessage.Chat.Id,
                            listMessage.MessageId,
                            "Done.",
                            cancellationToken: ct);

                        return Result.Handled();
                    }

                case "prev":
                    {
                        if (page > 0)
                            page--;

                        break;
                    }

                case "next":
                    {
                        if (page < maxPage)
                            page++;

                        break;
                    }
            }

            await bot.EditMessageText(
                listMessage.Chat.Id,
                listMessage.MessageId,
                FormatPageText(page, maxPage),
                replyMarkup: BuildKeyboardPage(page, maxPage),
                cancellationToken: ct);
        }
    }

    private static string FormatPageText(int page, int maxPage)
        => $"Items (page {page + 1} of {maxPage + 1}) :";

    private static InlineKeyboardMarkup BuildKeyboardPage(int page, int maxPage)
    {
        InlineKeyboardBuilder builder = new();

        int start = page * PageSize;
        int end = Math.Min(start + PageSize, Items.Length);

        for (int i = start; i < end; i++)
            builder.WithCallbackButton(Items[i], $"item:{Items[i]}");

        builder.WithCallbackButton("Prev", "prev");
        builder.WithCallbackButton("Done", "done");
        if (page < maxPage)
        {
            builder.WithCallbackButton("Next", "next");
            return builder.Adjust(PageSize, 3);
        }

        return builder.Adjust(PageSize, 2);
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
