using Telegram.Bot.Types;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the message text starts with the specified content.
/// </summary>
public sealed class TextStartsWithFilter : MessageFilter
{
    private readonly string _content;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextStartsWithFilter"/> class.
    /// </summary>
    /// <param name="content">The content to check if the message text starts with.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public TextStartsWithFilter(string content, StringComparison comparison = StringComparison.InvariantCulture)
    {
        _content = content;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => message.Text is { } text && text.StartsWith(_content, _comparison);
}

/// <summary>
/// Filter that checks if the message text ends with the specified content.
/// </summary>
public sealed class TextEndsWithFilter : MessageFilter
{
    private readonly string _content;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextEndsWithFilter"/> class.
    /// </summary>
    /// <param name="content">The content to check if the message text ends with.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public TextEndsWithFilter(string content, StringComparison comparison = StringComparison.InvariantCulture)
    {
        _content = content;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => message.Text is { } text && text.EndsWith(_content, _comparison);
}

/// <summary>
/// Filter that checks if the message text contains the specified content.
/// </summary>
public sealed class TextContainsFilter : MessageFilter
{
    private readonly string _content;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextContainsFilter"/> class.
    /// </summary>
    /// <param name="content">The content to check if the message text contains.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public TextContainsFilter(string content, StringComparison comparison = StringComparison.InvariantCulture)
    {
        _content = content;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => message.Text is { } text && text.IndexOf(_content, _comparison) >= 0;
}

/// <summary>
/// Filter that checks if the message text equals the specified content.
/// </summary>
public sealed class TextEqualsFilter : MessageFilter
{
    private readonly string _content;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextEqualsFilter"/> class.
    /// </summary>
    /// <param name="content">The content to check if the message text equals.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    public TextEqualsFilter(string content, StringComparison comparison = StringComparison.InvariantCulture)
    {
        _content = content;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => message.Text is { } text && text.Equals(_content, _comparison);
}

/// <summary>
/// Filter that checks if the message text is not null or empty.
/// </summary>
public sealed class TextNotNullOrEmptyFilter : MessageFilter
{
    /// <inheritdoc/>
    protected override bool CanPass(Message message) => !string.IsNullOrEmpty(message.Text);
}

/// <summary>
/// Filter that checks if the message text contains a 'word'.
/// 'Word' must be a separate member of the text, and not have any alphabetic characters next to it.
/// </summary>
public sealed class TextContainsWordFilter : MessageFilter
{
    private readonly string _word;
    private readonly StringComparison _comparison;
    private readonly int _startIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextContainsWordFilter"/> class.
    /// </summary>
    /// <param name="word">The word to check if the message text contains.</param>
    /// <param name="comparison">The string comparison type to use for the check.</param>
    /// <param name="startIndex">The search starting position.</param>
    public TextContainsWordFilter(string word, StringComparison comparison = StringComparison.InvariantCulture, int startIndex = 0)
    {
        _word = word;
        _comparison = comparison;
        _startIndex = startIndex;
    }

    /// <inheritdoc/>
    protected override bool CanPass(Message message)
        => message.Text is { } text && ContainsWord(text, _word, _comparison, _startIndex);

    private static bool ContainsWord(string source, string word, StringComparison comparison, int startIndex)
    {
        int index = source.IndexOf(word, startIndex, comparison);
        if (index == -1)
            return false;

        if (index > 0)
        {
            char prev = source[index - 1];
            if (char.IsLetter(prev))
                return false;
        }

        if (index + word.Length < source.Length)
        {
            char post = source[index + word.Length];
            if (char.IsLetter(post))
                return false;
        }

        return true;
    }
}
