using Telegram.Bot.Types;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the message sender's username equals the specified username.
/// </summary>
public sealed class FromUsernameFilter : MessageFilter
{
    private readonly string _username;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="FromUsernameFilter"/> class.
    /// </summary>
    /// <param name="username">The sender username to filter by.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public FromUsernameFilter(string username, StringComparison comparison = StringComparison.CurrentCulture)
    {
        _username = username;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => message.From is { } from && string.Equals(from.Username, _username, _comparison);
}

/// <summary>
/// Filter that checks if the message sender's first (and optionally last) name equals the specified names.
/// </summary>
public sealed class FromUserFilter : MessageFilter
{
    private readonly string _firstName;
    private readonly string? _lastName;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="FromUserFilter"/> class.
    /// </summary>
    /// <param name="firstName">The sender first name to filter by.</param>
    /// <param name="lastName">The sender last name to filter by, when provided.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public FromUserFilter(string firstName, string? lastName = null, StringComparison comparison = StringComparison.CurrentCulture)
    {
        _firstName = firstName;
        _lastName = lastName;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => message.From is { } from
           && string.Equals(from.FirstName, _firstName, _comparison)
           && (_lastName is null || string.Equals(from.LastName, _lastName, _comparison));
}

/// <summary>
/// Filter that checks if the message sender has the specified user id.
/// </summary>
public sealed class FromUserIdFilter : MessageFilter
{
    private readonly long _userId;

    /// <summary>
    /// Initializes a new instance of the <see cref="FromUserIdFilter"/> class.
    /// </summary>
    /// <param name="userId">The sender user id to filter by.</param>
    public FromUserIdFilter(long userId) => _userId = userId;

    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.From?.Id == _userId;
}

/// <summary>
/// Filter that checks if the message was sent by a bot.
/// </summary>
public sealed class FromBotFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.From?.IsBot == true;
}

/// <summary>
/// Filter that checks if the message was sent by a premium user.
/// </summary>
public sealed class FromPremiumUserFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => message.From?.IsPremium == true;
}
