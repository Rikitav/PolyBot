using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Filters;

/// <summary>
/// Enumeration of dice types supported by Telegram.
/// Used for filtering dice messages and determining dice emoji representations.
/// </summary>
public enum DiceType
{
    /// <summary>
    /// Standard dice (🎲).
    /// </summary>
    Dice,

    /// <summary>
    /// Darts (🎯).
    /// </summary>
    Darts,

    /// <summary>
    /// Bowling (🎳).
    /// </summary>
    Bowling,

    /// <summary>
    /// Basketball (🏀).
    /// </summary>
    Basketball,

    /// <summary>
    /// Football (⚽).
    /// </summary>
    Football,

    /// <summary>
    /// Casino slot machine (🎰).
    /// </summary>
    Casino
}

/// <summary>
/// Filters messages by their <see cref="MessageType"/>.
/// </summary>
public sealed class MessageTypeFilter : MessageFilter
{
    private readonly MessageType _type;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTypeFilter"/> class.
    /// </summary>
    /// <param name="type">The message type to filter by.</param>
    public MessageTypeFilter(MessageType type) => _type = type;

    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.Type == _type;
}

/// <summary>
/// Filters messages that are automatic forwards.
/// </summary>
public sealed class IsAutomaticForwardMessageFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.IsAutomaticForward;
}

/// <summary>
/// Filters messages that are sent from offline.
/// </summary>
public sealed class IsFromOfflineMessageFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.IsFromOffline;
}

/// <summary>
/// Filters service messages (e.g., chat events).
/// </summary>
public sealed class IsServiceMessageFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.IsServiceMessage;
}

/// <summary>
/// Filters "community chat added" service messages — emitted when a chat is added to a community.
/// </summary>
public sealed class IsCommunityChatAddedMessageFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.CommunityChatAdded is not null;
}

/// <summary>
/// Filters "community chat removed" service messages — emitted when a chat is removed from a community.
/// </summary>
public sealed class IsCommunityChatRemovedMessageFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.CommunityChatRemoved is not null;
}

/// <summary>
/// Filters messages that are topic messages.
/// </summary>
public sealed class IsTopicMessageFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.IsTopicMessage;
}

/// <summary>
/// Filters messages by dice throw value and optionally by dice type.
/// When constructed with only a value, the dice type (emoji) is not checked.
/// </summary>
public sealed class DiceThrowedFilter : MessageFilter
{
    private readonly DiceType? _diceType;
    private readonly int _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiceThrowedFilter"/> class for a specific value.
    /// </summary>
    /// <param name="value">The dice value to filter by.</param>
    public DiceThrowedFilter(int value) => _value = value;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiceThrowedFilter"/> class for a specific dice type and value.
    /// </summary>
    /// <param name="diceType">The dice type to filter by.</param>
    /// <param name="value">The dice value to filter by.</param>
    public DiceThrowedFilter(DiceType diceType, int value)
    {
        _diceType = diceType;
        _value = value;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
    {
        if (message.Dice is not { } dice)
            return false;

        if (_diceType is { } diceType && dice.Emoji != GetEmojyForDiceType(diceType))
            return false;

        return dice.Value == _value;
    }

    private static string? GetEmojyForDiceType(DiceType? diceType) => diceType switch
    {
        DiceType.Dice => "🎲",
        DiceType.Darts => "🎯",
        DiceType.Bowling => "🎳",
        DiceType.Basketball => "🏀",
        DiceType.Football => "⚽",
        DiceType.Casino => "🎰",
        _ => null
    };
}

/// <summary>
/// Filters messages by matching their text with a regular expression.
/// </summary>
public sealed class MessageRegexFilter : MessageFilter
{
    private readonly Regex _regex;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRegexFilter"/> class with a pattern and options.
    /// </summary>
    /// <param name="pattern">The regex pattern.</param>
    /// <param name="regexOptions">The regex options.</param>
    public MessageRegexFilter(string pattern, RegexOptions regexOptions = default)
        => _regex = new Regex(pattern, regexOptions);

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRegexFilter"/> class with a regex object.
    /// </summary>
    /// <param name="regex">The regex object.</param>
    public MessageRegexFilter(Regex regex) => _regex = regex;

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => message.Text is { Length: > 0 } text && _regex.IsMatch(text);
}

/// <summary>
/// Filters messages that contain a specific entity type, content, offset, or length.
/// </summary>
public sealed class MessageHasEntityFilter : MessageFilter
{
    private readonly MessageEntityType _entityType;
    private readonly string? _content;
    private readonly int? _offset;
    private readonly int? _length;
    private readonly StringComparison _stringComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHasEntityFilter"/> class for a specific entity type.
    /// </summary>
    /// <param name="type">The entity type to filter by.</param>
    public MessageHasEntityFilter(MessageEntityType type)
    {
        _entityType = type;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHasEntityFilter"/> class for a specific entity type, offset, and length.
    /// </summary>
    /// <param name="type">The entity type to filter by.</param>
    /// <param name="offset">The offset to filter by.</param>
    /// <param name="length">The length to filter by.</param>
    public MessageHasEntityFilter(MessageEntityType type, int? offset, int? length)
    {
        _entityType = type;
        _offset = offset;
        _length = length;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHasEntityFilter"/> class for a specific entity type and content.
    /// </summary>
    /// <param name="type">The entity type to filter by.</param>
    /// <param name="content">The content to filter by.</param>
    /// <param name="stringComparison">The string comparison to use.</param>
    public MessageHasEntityFilter(MessageEntityType type, string? content, StringComparison stringComparison = StringComparison.CurrentCulture)
    {
        _entityType = type;
        _content = content;
        _stringComparison = stringComparison;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHasEntityFilter"/> class for a specific entity type, offset, length, and content.
    /// </summary>
    /// <param name="type">The entity type to filter by.</param>
    /// <param name="offset">The offset to filter by.</param>
    /// <param name="length">The length to filter by.</param>
    /// <param name="content">The content to filter by.</param>
    /// <param name="stringComparison">The string comparison to use.</param>
    public MessageHasEntityFilter(MessageEntityType type, int? offset, int? length, string? content, StringComparison stringComparison = StringComparison.CurrentCulture)
    {
        _entityType = type;
        _offset = offset;
        _length = length;
        _content = content;
        _stringComparison = stringComparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
    {
        if (message.Entities is not { Length: > 0 } entities)
            return false;

        foreach (MessageEntity entity in entities)
        {
            if (FilterEntity(message.Text, entity))
                return true;
        }

        return false;
    }

    private bool FilterEntity(string? text, MessageEntity entity)
    {
        if (entity.Type != _entityType)
            return false;

        if (_offset != null && entity.Offset != _offset)
            return false;

        if (_length != null && entity.Length != _length)
            return false;

        if (_content != null)
        {
            if (text is not { Length: > 0 })
                return false;

            if (!text.Substring(entity.Offset, entity.Length).Equals(_content, _stringComparison))
                return false;
        }

        return true;
    }
}
