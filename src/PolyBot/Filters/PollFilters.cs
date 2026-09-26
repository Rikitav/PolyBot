using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the poll has the specified <see cref="PollType"/>.
/// </summary>
public sealed class PollTypeFilter : PollFilter
{
    private readonly PollType _pollType;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollTypeFilter"/> class.
    /// </summary>
    /// <param name="pollType">The poll type to filter by.</param>
    public PollTypeFilter(PollType pollType) => _pollType = pollType;

    /// <inheritdoc/>
    protected override bool CanPass(Poll poll) => poll.Type == _pollType;
}

/// <summary>
/// Filter that checks if the poll is closed.
/// </summary>
public sealed class PollIsClosedFilter : PollFilter
{
    private readonly bool _isClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollIsClosedFilter"/> class.
    /// </summary>
    /// <param name="isClosed">The closed state to filter by (default: <c>true</c>).</param>
    public PollIsClosedFilter(bool isClosed = true) => _isClosed = isClosed;

    /// <inheritdoc/>
    protected override bool CanPass(Poll poll) => poll.IsClosed == _isClosed;
}

/// <summary>
/// Filter that checks if the poll is anonymous.
/// </summary>
public sealed class PollIsAnonymousFilter : PollFilter
{
    private readonly bool _isAnonymous;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollIsAnonymousFilter"/> class.
    /// </summary>
    /// <param name="isAnonymous">The anonymous state to filter by (default: <c>true</c>).</param>
    public PollIsAnonymousFilter(bool isAnonymous = true) => _isAnonymous = isAnonymous;

    /// <inheritdoc/>
    protected override bool CanPass(Poll poll) => poll.IsAnonymous == _isAnonymous;
}
