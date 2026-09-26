using Telegram.Bot.Types;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the chosen inline result has the specified result id.
/// </summary>
public sealed class ChosenInlineResultIdFilter : ChosenInlineResultFilter
{
    private readonly string _resultId;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChosenInlineResultIdFilter"/> class.
    /// </summary>
    /// <param name="resultId">The result id to filter by.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public ChosenInlineResultIdFilter(string resultId, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _resultId = resultId;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(ChosenInlineResult result)
        => result.ResultId is { } resultId && resultId.Equals(_resultId, _comparison);
}

/// <summary>
/// Filter that checks if the chosen inline result has the specified query.
/// </summary>
public sealed class ChosenInlineResultQueryFilter : ChosenInlineResultFilter
{
    private readonly string _query;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChosenInlineResultQueryFilter"/> class.
    /// </summary>
    /// <param name="query">The query to filter by.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public ChosenInlineResultQueryFilter(string query, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _query = query;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(ChosenInlineResult result)
        => result.Query is { } query && query.Equals(_query, _comparison);
}
