using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the message chat is a forum.
/// </summary>
public sealed class ChatIsForumFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.Chat?.IsForum == true;
}

/// <summary>
/// Filter that checks if the message chat has the specified id.
/// </summary>
public sealed class MessageChatIdFilter : MessageFilter
{
    private readonly long _id;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageChatIdFilter"/> class.
    /// </summary>
    /// <param name="id">The chat id to filter by.</param>
    public MessageChatIdFilter(long id) => _id = id;

    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.Chat?.Id == _id;
}

/// <summary>
/// Filter that checks if the message chat has the specified <see cref="ChatType"/>.
/// </summary>
public sealed class ChatTypeFilter : MessageFilter
{
    private readonly ChatType _type;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatTypeFilter"/> class.
    /// </summary>
    /// <param name="type">The chat type to filter by.</param>
    public ChatTypeFilter(ChatType type) => _type = type;

    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.Chat?.Type == _type;
}

/// <summary>
/// Filter that checks if the message chat title equals the specified title.
/// </summary>
public sealed class ChatTitleFilter : MessageFilter
{
    private readonly string? _title;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatTitleFilter"/> class.
    /// </summary>
    /// <param name="title">The chat title to filter by.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public ChatTitleFilter(string? title, StringComparison comparison = StringComparison.CurrentCulture)
    {
        _title = title;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => string.Equals(message.Chat?.Title, _title, _comparison);
}

/// <summary>
/// Filter that checks if the message chat username equals the specified username.
/// </summary>
public sealed class ChatUsernameFilter : MessageFilter
{
    private readonly string? _username;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatUsernameFilter"/> class.
    /// </summary>
    /// <param name="username">The chat username to filter by.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public ChatUsernameFilter(string? username, StringComparison comparison = StringComparison.CurrentCulture)
    {
        _username = username;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => string.Equals(message.Chat?.Username, _username, _comparison);
}

/// <summary>
/// Filter that checks if the message chat first and last names equal the specified names.
/// </summary>
public sealed class ChatNameFilter : MessageFilter
{
    private readonly string? _firstName;
    private readonly string? _lastName;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatNameFilter"/> class.
    /// </summary>
    /// <param name="firstName">The chat first name to filter by.</param>
    /// <param name="lastName">The chat last name to filter by.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public ChatNameFilter(string? firstName, string? lastName, StringComparison comparison = StringComparison.CurrentCulture)
    {
        _firstName = firstName;
        _lastName = lastName;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => string.Equals(message.Chat?.FirstName, _firstName, _comparison)
           && string.Equals(message.Chat?.LastName, _lastName, _comparison);
}
