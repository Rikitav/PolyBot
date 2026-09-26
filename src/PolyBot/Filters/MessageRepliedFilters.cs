using Telegram.Bot.Types;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the message has a reply chain of the specified depth.
/// </summary>
public sealed class MessageHasReplyFilter : MessageFilter
{
    private readonly int _replyDepth;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHasReplyFilter"/> class.
    /// </summary>
    /// <param name="replyDepth">The depth of the reply chain to traverse (default: 1).</param>
    public MessageHasReplyFilter(int replyDepth = 1) => _replyDepth = replyDepth;

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
    {
        Message current = message;
        for (int i = 0; i < _replyDepth; i++)
        {
            if (current.ReplyToMessage is not { Id: > 0 } reply)
                return false;

            current = reply;
        }

        return true;
    }
}

/// <summary>
/// Filter that checks if the directly replied message was sent by the bot itself.
/// </summary>
public sealed class MeRepliedFilter : MessageFilter
{
    private readonly PolyBotOptions? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeRepliedFilter"/> class.
    /// </summary>
    /// <param name="options">The bot options carrying the bot username; when omitted, the filter never passes.</param>
    public MeRepliedFilter(PolyBotOptions? options = null) => _options = options;

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
    {
        string? repliedUsername = message.ReplyToMessage?.From?.Username;
        string? botUsername = _options?.BotUsername;
        if (repliedUsername is null || botUsername is null)
            return false;

        return string.Equals(
            repliedUsername.TrimStart('@'),
            botUsername.TrimStart('@'),
            StringComparison.OrdinalIgnoreCase);
    }
}
