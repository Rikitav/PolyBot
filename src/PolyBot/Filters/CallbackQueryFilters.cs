using System.Text.RegularExpressions;
using Telegram.Bot.Types;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the callback query data equals the specified data.
/// </summary>
public sealed class CallbackDataFilter : CallbackQueryFilter
{
    private readonly string _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackDataFilter"/> class.
    /// </summary>
    /// <param name="data">The callback data to filter by.</param>
    public CallbackDataFilter(string data) => _data = data;

    /// <inheritdoc/>
    protected override bool CanPass(CallbackQuery query) => query.Data == _data;
}

/// <summary>
/// Filter that checks if the callback query data contains the specified value.
/// </summary>
public sealed class CallbackDataContainsFilter : CallbackQueryFilter
{
    private readonly string _value;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackDataContainsFilter"/> class.
    /// </summary>
    /// <param name="value">The value to check if the callback data contains.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public CallbackDataContainsFilter(string value, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _value = value;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(CallbackQuery query)
        => query.Data is { } data && data.IndexOf(_value, _comparison) >= 0;
}

/// <summary>
/// Filter that checks if the callback query data starts with the specified value.
/// </summary>
public sealed class CallbackDataStartsWithFilter : CallbackQueryFilter
{
    private readonly string _value;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackDataStartsWithFilter"/> class.
    /// </summary>
    /// <param name="value">The value to check if the callback data starts with.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public CallbackDataStartsWithFilter(string value, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _value = value;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(CallbackQuery query)
        => query.Data is { } data && data.StartsWith(_value, _comparison);
}

/// <summary>
/// Filter that checks if the callback query data ends with the specified value.
/// </summary>
public sealed class CallbackDataEndsWithFilter : CallbackQueryFilter
{
    private readonly string _value;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackDataEndsWithFilter"/> class.
    /// </summary>
    /// <param name="value">The value to check if the callback data ends with.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public CallbackDataEndsWithFilter(string value, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        _value = value;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(CallbackQuery query)
        => query.Data is { } data && data.EndsWith(_value, _comparison);
}

/// <summary>
/// Filter that checks if the callback query belongs to the specified inline message.
/// </summary>
public sealed class CallbackInlineIdFilter : CallbackQueryFilter
{
    private readonly string _inlineMessageId;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackInlineIdFilter"/> class.
    /// </summary>
    /// <param name="inlineMessageId">The inline message id to filter by.</param>
    public CallbackInlineIdFilter(string inlineMessageId) => _inlineMessageId = inlineMessageId;

    /// <inheritdoc/>
    protected override bool CanPass(CallbackQuery query) => query.InlineMessageId == _inlineMessageId;
}

/// <summary>
/// Filter that checks if the callback query data matches a regular expression.
/// </summary>
public sealed class CallbackRegexFilter : CallbackQueryFilter
{
    private readonly Regex _regex;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRegexFilter"/> class with a pattern and options.
    /// </summary>
    /// <param name="pattern">The regex pattern.</param>
    /// <param name="regexOptions">The regex options.</param>
    public CallbackRegexFilter(string pattern, RegexOptions regexOptions = default)
        => _regex = new Regex(pattern, regexOptions);

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRegexFilter"/> class with a regex object.
    /// </summary>
    /// <param name="regex">The regex object.</param>
    public CallbackRegexFilter(Regex regex) => _regex = regex;

    /// <inheritdoc/>
    protected override bool CanPass(CallbackQuery query)
        => query.Data is { Length: > 0 } data && _regex.IsMatch(data);
}
