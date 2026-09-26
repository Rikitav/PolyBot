using System.Text.RegularExpressions;
using Telegram.Bot.Types;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the inline query text equals the specified text.
/// </summary>
public sealed class InlineQueryTextFilter : InlineQueryFilter
{
    private readonly string _text;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineQueryTextFilter"/> class.
    /// </summary>
    /// <param name="text">The text to filter by.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public InlineQueryTextFilter(string text, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _text = text;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(InlineQuery inlineQuery)
        => inlineQuery.Query is { } query && query.Equals(_text, _comparison);
}

/// <summary>
/// Filter that checks if the inline query text contains the specified text.
/// </summary>
public sealed class InlineQueryContainsFilter : InlineQueryFilter
{
    private readonly string _text;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineQueryContainsFilter"/> class.
    /// </summary>
    /// <param name="text">The text to check if the inline query contains.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public InlineQueryContainsFilter(string text, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _text = text;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(InlineQuery inlineQuery)
        => inlineQuery.Query is { } query && query.IndexOf(_text, _comparison) >= 0;
}

/// <summary>
/// Filter that checks if the inline query text starts with the specified text.
/// </summary>
public sealed class InlineQueryStartsWithFilter : InlineQueryFilter
{
    private readonly string _text;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineQueryStartsWithFilter"/> class.
    /// </summary>
    /// <param name="text">The text to check if the inline query starts with.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public InlineQueryStartsWithFilter(string text, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _text = text;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(InlineQuery inlineQuery)
        => inlineQuery.Query is { } query && query.StartsWith(_text, _comparison);
}

/// <summary>
/// Filter that checks if the inline query text ends with the specified text.
/// </summary>
public sealed class InlineQueryEndsWithFilter : InlineQueryFilter
{
    private readonly string _text;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineQueryEndsWithFilter"/> class.
    /// </summary>
    /// <param name="text">The text to check if the inline query ends with.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public InlineQueryEndsWithFilter(string text, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _text = text;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(InlineQuery inlineQuery)
        => inlineQuery.Query is { } query && query.EndsWith(_text, _comparison);
}

/// <summary>
/// Filter that checks if the inline query text matches a regular expression.
/// </summary>
public sealed class InlineQueryRegexFilter : InlineQueryFilter
{
    private readonly Regex _regex;

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineQueryRegexFilter"/> class with a pattern and options.
    /// </summary>
    /// <param name="pattern">The regex pattern.</param>
    /// <param name="regexOptions">The regex options.</param>
    public InlineQueryRegexFilter(string pattern, RegexOptions regexOptions = default)
        => _regex = new Regex(pattern, regexOptions);

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineQueryRegexFilter"/> class with a regex object.
    /// </summary>
    /// <param name="regex">The regex object.</param>
    public InlineQueryRegexFilter(Regex regex) => _regex = regex;

    /// <inheritdoc/>
    protected override bool CanPass(InlineQuery inlineQuery)
        => inlineQuery.Query is { Length: > 0 } query && _regex.IsMatch(query);
}
