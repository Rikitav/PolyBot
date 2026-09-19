using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot;

/// <summary>
/// Test-harness controller returned by <see cref="PolyBotClient.RunTest"/>: pushes synthetic
/// updates straight into the generated router — no Telegram polling. <see cref="BotClient"/>
/// exposes <c>SentRequests</c> for outgoing-request assertions.
/// </summary>
public sealed class UpdateMocker
{
    private readonly IUpdateHandler _handler;
    private int _nextUpdateId;
    private int _nextMessageId;

    internal UpdateMocker(IServiceProvider services, IUpdateHandler handler, PolyTests botClient)
    {
        Services = services;
        _handler = handler;
        BotClient = botClient;
    }

    /// <summary>
    /// The built service provider of the test host.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// The in-memory bot client serving this harness.
    /// </summary>
    public PolyTests BotClient { get; }

    /// <summary>
    /// Pushes an arbitrary update into the router, assigning an id when unset.
    /// </summary>
    public async Task<Update> Update(Update update, CancellationToken cancellationToken = default)
    {
        if (update.Id == 0)
        {
            update.Id = Interlocked.Increment(ref _nextUpdateId);
        }

        await _handler.HandleUpdateAsync(BotClient, update, cancellationToken).ConfigureAwait(false);
        return update;
    }

    /// <summary>
    /// Pushes a plain text message.
    /// </summary>
    public Task<Update> Message(string text, long userId = 42, long chatId = 1, CancellationToken cancellationToken = default)
    {
        return Update(new Update
        {
            Message = new Message
            {
                Id = Interlocked.Increment(ref _nextMessageId),
                Text = text,
                From = new User { Id = userId, FirstName = "Test" },
                Chat = new Chat { Id = chatId, Type = ChatType.Private },
            },
        }, cancellationToken);
    }

    /// <summary>
    /// Pushes a bot-command message (leading '/' optional; the entity is created automatically).
    /// </summary>
    public Task<Update> Command(string command, long userId = 42, long chatId = 1, CancellationToken cancellationToken = default)
    {
        string text = command.StartsWith("/", StringComparison.Ordinal) ? command : "/" + command;
        return Update(new Update
        {
            Message = new Message
            {
                Id = Interlocked.Increment(ref _nextMessageId),
                Text = text,
                Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = text.Length }],
                From = new User { Id = userId, FirstName = "Test" },
                Chat = new Chat { Id = chatId, Type = ChatType.Private },
            },
        }, cancellationToken);
    }

    /// <summary>
    /// Pushes a callback query with the given data.
    /// </summary>
    public Task<Update> Callback(string data, long userId = 42, CancellationToken cancellationToken = default)
    {
        return Update(new Update
        {
            CallbackQuery = new CallbackQuery
            {
                Id = "mock-" + Interlocked.Increment(ref _nextUpdateId).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Data = data,
                From = new User { Id = userId, FirstName = "Test" },
            },
        }, cancellationToken);
    }

    /// <summary>
    /// Pushes an inline query.
    /// </summary>
    public Task<Update> InlineQuery(string query, long userId = 42, CancellationToken cancellationToken = default)
    {
        return Update(new Update
        {
            InlineQuery = new InlineQuery
            {
                Id = "mock-query",
                Query = query,
                From = new User { Id = userId, FirstName = "Test" },
            },
        }, cancellationToken);
    }
}
