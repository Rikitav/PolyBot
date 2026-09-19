namespace PolyBot.Routing;

/// <summary>
/// Zero-allocation segment matcher for <c>CallbackQuery.Data</c> route templates. The
/// generated router calls <see cref="Match"/> once per pattern-guarded handler and then
/// tests segments by index; segment bounds are resolved by scanning, so no offset arrays
/// are allocated per update.
/// </summary>
public static class CallbackPatternMatcher
{
    /// <summary>
    /// Telegram's <c>callback_data</c> byte limit.
    /// </summary>
    public const int MaxCallbackDataBytes = 64;

    /// <summary>
    /// Splits <paramref name="data"/> into segments and succeeds when at least
    /// <paramref name="minSegments"/> are present; fails past what a 64-byte payload can contain.
    /// </summary>
    public static bool Match(string data, char separator, int minSegments, out CallbackSegments segments)
    {
        int count = 1;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == separator)
            {
                count++;
                if (count > 32)
                {
                    segments = default;
                    return false;
                }
            }
        }

        segments = new CallbackSegments(data, separator, count);
        return count >= minSegments;
    }
}

/// <summary>
/// Segment view over callback data produced by <see cref="CallbackPatternMatcher.Match"/>;
/// segment bounds are resolved by scanning on each access.
/// </summary>
public readonly struct CallbackSegments
{
    private readonly string _data;
    private readonly char _separator;

    internal CallbackSegments(string data, char separator, int count)
    {
        _data = data;
        _separator = separator;
        Count = count;
    }

    /// <summary>
    /// The number of segments (always at least 1 for non-empty data).
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Ordinal comparison of the segment at <paramref name="index"/> against a literal.
    /// </summary>
    public bool EqualsOrdinal(int index, string literal)
    {
        BoundsOf(index, out int offset, out int length);
        return length == literal.Length && string.CompareOrdinal(_data, offset, literal, 0, literal.Length) == 0;
    }

    /// <summary>
    /// Extracts the segment at <paramref name="index"/> (allocates the substring).
    /// </summary>
    public string Substring(int index)
    {
        BoundsOf(index, out int offset, out int length);
        return _data.Substring(offset, length);
    }

    /// <summary>
    /// Extracts everything from the segment at <paramref name="startIndex"/> to the end,
    /// separators included — the value of a trailing <c>{*name}</c> wildcard.
    /// </summary>
    public string JoinRest(int startIndex)
    {
        BoundsOf(startIndex, out int offset, out _);
        return _data.Substring(offset);
    }

    private void BoundsOf(int index, out int offset, out int length)
    {
        offset = 0;
        for (int i = 0; i < index; i++)
        {
            offset = _data.IndexOf(_separator, offset) + 1;
        }

        int end = _data.IndexOf(_separator, offset);
        length = end < 0 ? _data.Length - offset : end - offset;
    }
}
