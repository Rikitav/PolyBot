using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if a message contains a mention of the bot or a specific user.
/// </summary>
public sealed class MentionedFilter : MessageFilter
{
    private readonly string? _mention;
    private readonly PolyBotOptions? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MentionedFilter"/> class.
    /// </summary>
    /// <param name="mention">The username to check for in the mention; when omitted, the bot's username is used.</param>
    /// <param name="options">The bot options carrying the bot username.</param>
    public MentionedFilter(string? mention = null, PolyBotOptions? options = null)
    {
        _mention = mention;
        _options = options;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
    {
        string? mention = _mention?.TrimStart('@');
        string? botUsername = _options?.BotUsername?.TrimStart('@');
        if (mention is null && botUsername is null)
            return false;

        if (message.Text is not { } text || message.Entities is not { Length: > 0 } entities)
            return false;

        foreach (MessageEntity entity in entities)
        {
            if (entity.Type != MessageEntityType.Mention)
                continue;

            string candidate = text.Substring(entity.Offset, entity.Length).TrimStart('@');
            if (string.Equals(candidate, mention, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, botUsername, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
