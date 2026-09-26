using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace PolyBot.Routing;

/// <summary>
/// Zero-allocation segment matcher for <c>CallbackQuery.Data</c> route templates. The
/// generated router calls <see cref="Match(string, char, int, out CallbackSegments)"/> once per pattern-guarded handler and then
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

    /// <summary>
    /// Matches <paramref name="data"/> against a literal-only template (no <c>{name}</c>
    /// placeholders), e.g. an exact <c>"cart:checkout"</c> route — the imperative twin of a
    /// <c>[Pattern("cart:checkout")]</c> handler without captures. Uses the default <c>':'</c>
    /// separator; the overload taking a <c>char</c> accepts any separator.
    /// </summary>
    /// <exception cref="ArgumentException">The template declares placeholders; use the
    /// generic overloads to receive captures.</exception>
    public static bool Match(string data, string template)
        => Match(data, ':', template);

    /// <inheritdoc cref="Match(string, string)" />
    public static bool Match(string data, char separator, string template)
        => MatchTemplate(data, separator, template, Array.Empty<string?>(), typeof(string));

    /// <summary>
    /// Matches <paramref name="data"/> against a <c>[Pattern]</c>-style segment template and
    /// extracts one typed capture positionally — the imperative twin of
    /// <c>[Pattern("item:{id}")]</c> for code that awaits callbacks inline via
    /// <c>IUpdateAwaiter</c>. Uses the default <c>':'</c> separator; overloads taking a
    /// <c>char</c> accept any separator.
    /// </summary>
    /// <typeparam name="T1">Capture type of the template's single <c>{name}</c> placeholder; see
    /// the conversion rules in <c>docs/features/callback-patterns.mdx</c> (raw text for
    /// <see cref="string"/>, case-insensitive parse for enums, invariant-culture parse for
    /// numerics, otherwise a static <c>TryParse(string, out T)</c>).</typeparam>
    /// <returns><see langword="true"/> when every literal segment matches and all captures
    /// convert; otherwise <see langword="false"/> with <paramref name="arg1"/> set to
    /// <see langword="default"/>.</returns>
    /// <exception cref="ArgumentException">The template is malformed, declares a different
    /// number of placeholders than output arguments, or binds a <c>{*name}</c> wildcard to a
    /// non-<see cref="string"/> argument.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="T1"/> has no accessible
    /// static <c>TryParse</c> (and is not <see cref="string"/> or an enum).</exception>
    public static bool Match<T1>(string data, string template, out T1 arg1)
        => Match(data, ':', template, out arg1);

    /// <inheritdoc cref="Match{T1}(string, string, out T1)" />
    public static bool Match<T1>(string data, char separator, string template, out T1 arg1)
    {
        string?[] captures = new string?[1];
        if (!MatchTemplate(data, separator, template, captures, typeof(T1)) || !TryConvert(captures[0], out arg1))
        {
            arg1 = default!;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Positional two-capture variant of <see cref="Match{T1}(string, string, out T1)"/>;
    /// generic arguments correspond to <c>{name}</c> placeholders in template order.
    /// </summary>
    public static bool Match<T1, T2>(string data, string template, out T1 arg1, out T2 arg2)
        => Match(data, ':', template, out arg1, out arg2);

    /// <inheritdoc cref="Match{T1, T2}(string, string, out T1, out T2)" />
    public static bool Match<T1, T2>(string data, char separator, string template, out T1 arg1, out T2 arg2)
    {
        string?[] captures = new string?[2];
        if (!MatchTemplate(data, separator, template, captures, typeof(T2)) ||
            !TryConvert(captures[0], out arg1) ||
            !TryConvert(captures[1], out arg2))
        {
            arg1 = default!;
            arg2 = default!;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Positional three-capture variant of <see cref="Match{T1}(string, string, out T1)"/>;
    /// generic arguments correspond to <c>{name}</c> placeholders in template order.
    /// </summary>
    public static bool Match<T1, T2, T3>(string data, string template, out T1 arg1, out T2 arg2, out T3 arg3)
        => Match(data, ':', template, out arg1, out arg2, out arg3);

    /// <inheritdoc cref="Match{T1, T2, T3}(string, string, out T1, out T2, out T3)" />
    public static bool Match<T1, T2, T3>(string data, char separator, string template, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        string?[] captures = new string?[3];
        if (!MatchTemplate(data, separator, template, captures, typeof(T3)) ||
            !TryConvert(captures[0], out arg1) ||
            !TryConvert(captures[1], out arg2) ||
            !TryConvert(captures[2], out arg3))
        {
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Positional four-capture variant of <see cref="Match{T1}(string, string, out T1)"/>;
    /// generic arguments correspond to <c>{name}</c> placeholders in template order.
    /// </summary>
    public static bool Match<T1, T2, T3, T4>(string data, string template, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
        => Match(data, ':', template, out arg1, out arg2, out arg3, out arg4);

    /// <inheritdoc cref="Match{T1, T2, T3, T4}(string, string, out T1, out T2, out T3, out T4)" />
    public static bool Match<T1, T2, T3, T4>(string data, char separator, string template, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        string?[] captures = new string?[4];
        if (!MatchTemplate(data, separator, template, captures, typeof(T4)) ||
            !TryConvert(captures[0], out arg1) ||
            !TryConvert(captures[1], out arg2) ||
            !TryConvert(captures[2], out arg3) ||
            !TryConvert(captures[3], out arg4))
        {
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Positional five-capture variant of <see cref="Match{T1}(string, string, out T1)"/>;
    /// generic arguments correspond to <c>{name}</c> placeholders in template order.
    /// </summary>
    public static bool Match<T1, T2, T3, T4, T5>(string data, string template, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
        => Match(data, ':', template, out arg1, out arg2, out arg3, out arg4, out arg5);

    /// <inheritdoc cref="Match{T1, T2, T3, T4, T5}(string, string, out T1, out T2, out T3, out T4, out T5)" />
    public static bool Match<T1, T2, T3, T4, T5>(string data, char separator, string template, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        string?[] captures = new string?[5];
        if (!MatchTemplate(data, separator, template, captures, typeof(T5)) ||
            !TryConvert(captures[0], out arg1) ||
            !TryConvert(captures[1], out arg2) ||
            !TryConvert(captures[2], out arg3) ||
            !TryConvert(captures[3], out arg4) ||
            !TryConvert(captures[4], out arg5))
        {
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            return false;
        }

        return true;
    }

    /// <summary>
    /// A 64-byte <c>callback_data</c> payload can hold at most 32 separators; shared with
    /// the generated router's guard in <see cref="Match(string, char, int, out CallbackSegments)"/>.
    /// </summary>
    private const int MaxSegments = 32;

    private delegate bool TryParseStringDelegate<T>(string value, out T result);

    private delegate bool TryParseSpanDelegate<T>(ReadOnlySpan<char> value, out T result);

    private delegate bool EnumTryParseDelegate(string value, out object? result);

    private static readonly ConcurrentDictionary<Type, Delegate?> CustomTryParseDelegates = new();

    private static readonly ConcurrentDictionary<Type, EnumTryParseDelegate> EnumTryParseDelegates = new();

    /// <summary>
    /// Parses an enum capture case-insensitively through a cached closed-generic delegate;
    /// the <c>Enum.TryParse(Type, string, bool, out object)</c> overload is unavailable on
    /// netstandard2.0, so <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> is
    /// closed over the capture type instead.
    /// </summary>
    private static bool TryParseEnum(Type type, string value, out object? result)
    {
        EnumTryParseDelegate parser = EnumTryParseDelegates.GetOrAdd(type, static t =>
            (EnumTryParseDelegate)Delegate.CreateDelegate(
                typeof(EnumTryParseDelegate),
                typeof(CallbackPatternMatcher)
                    .GetMethod(nameof(EnumTryParseCore), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(t)));
        return parser(value, out result);
    }

    private static bool EnumTryParseCore<TEnum>(string value, out object? result)
        where TEnum : struct
    {
        if (Enum.TryParse<TEnum>(value, true, out TEnum parsed))
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Splits a template on separators that appear outside <c>{...}</c> groups, so inline
    /// constraints like <c>{id:int}</c> survive a <c>':'</c> separator — the same rule the
    /// source generator applies to <c>[Pattern]</c> templates.
    /// </summary>
    private static List<string> SplitTemplate(string template, char separator)
    {
        List<string> parts = new();
        int start = 0;
        int depth = 0;
        for (int i = 0; i < template.Length; i++)
        {
            char c = template[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
            }
            else if (c == separator && depth == 0)
            {
                parts.Add(template.Substring(start, i - start));
                start = i + 1;
            }
        }

        parts.Add(template.Substring(start));
        return parts;
    }

    /// <summary>
    /// Validates <paramref name="template"/> and, when it matches <paramref name="data"/>,
    /// fills <paramref name="captures"/> with the raw segment values (the wildcard tail, when
    /// present, as the remaining text with separators included). Mirrors the generated
    /// router's guard: exact segment count unless a <c>{*name}</c> wildcard is the last
    /// segment, in which case <paramref name="data"/> may have more segments than the template.
    /// </summary>
    private static bool MatchTemplate(string data, char separator, string template, string?[] captures, Type lastCaptureType)
    {
        int dataCount = 1;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == separator && ++dataCount > MaxSegments)
            {
                return false;
            }
        }

        List<string> parts = SplitTemplate(template, separator);
        if (parts.Count > MaxSegments)
        {
            throw new ArgumentException($"The template declares more than {MaxSegments} segments and can never fit into callback_data.", nameof(template));
        }

        int captureCount = 0;
        int wildcardIndex = -1;
        for (int i = 0; i < parts.Count; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
            {
                throw new ArgumentException("The template contains an empty segment; empty segments are not allowed.", nameof(template));
            }

            if (part[0] != '{')
            {
                if (part.IndexOf('}') >= 0)
                {
                    throw new ArgumentException($"Literal segment '{part}' contains a brace.", nameof(template));
                }

                continue;
            }

            if (part.Length < 3 || part[part.Length - 1] != '}')
            {
                throw new ArgumentException($"Segment '{part}' has unbalanced braces.", nameof(template));
            }

            string inner = part.Substring(1, part.Length - 2);
            if (inner[0] == '*')
            {
                if (inner.Length == 1)
                {
                    throw new ArgumentException("A wildcard '{*name}' segment needs a name.", nameof(template));
                }

                if (i != parts.Count - 1)
                {
                    throw new ArgumentException("A wildcard '{*name}' segment must be the last template segment.", nameof(template));
                }

                wildcardIndex = i;
            }
            else
            {
                int colon = inner.IndexOf(':');
                if (colon == 0 || (colon >= 0 && colon == inner.Length - 1))
                {
                    throw new ArgumentException($"Segment '{part}' has an empty placeholder or constraint.", nameof(template));
                }
            }

            captureCount++;
        }

        if (captureCount != captures.Length)
        {
            throw new ArgumentException($"The template declares {captureCount} placeholder(s) but {captures.Length} output argument(s) were provided.", nameof(template));
        }

        if (wildcardIndex >= 0 && lastCaptureType != typeof(string))
        {
            throw new ArgumentException("A wildcard '{*name}' capture binds the remaining data as text and requires a string output argument.", nameof(template));
        }

        int minSegments = wildcardIndex >= 0 ? parts.Count - 1 : parts.Count;
        if (dataCount < minSegments || (wildcardIndex < 0 && dataCount != parts.Count))
        {
            return false;
        }

        int dataOffset = 0;
        int slot = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            string part = parts[i];

            int dataEnd = data.IndexOf(separator, dataOffset);
            if (dataEnd < 0)
            {
                dataEnd = data.Length;
            }

            if (part[0] == '{')
            {
                if (i == wildcardIndex)
                {
                    captures[slot++] = data.Substring(dataOffset);
                    return true;
                }

                captures[slot++] = data.Substring(dataOffset, dataEnd - dataOffset);
            }
            else
            {
                int dataLength = dataEnd - dataOffset;
                if (dataLength != part.Length || string.CompareOrdinal(data, dataOffset, part, 0, part.Length) != 0)
                {
                    return false;
                }
            }

            dataOffset = dataEnd + 1;
        }

        return true;
    }

    /// <summary>
    /// Converts a raw captured segment to <typeparamref name="T"/> using the same rules as
    /// the generated router: <see cref="string"/> binds verbatim, enums parse
    /// case-insensitively, BCL numerics parse with <see cref="NumberStyles.Any"/> and an
    /// invariant culture, any other type needs an accessible static
    /// <c>TryParse(string, out T)</c> (or <c>TryParse(ReadOnlySpan&lt;char&gt;, out T)</c>).
    /// </summary>
    private static bool TryConvert<T>(string? value, out T result)
    {
        result = default!;
        if (value is null)
        {
            return false;
        }

        Type type = typeof(T);
        if (type == typeof(string))
        {
            result = (T)(object)value;
            return true;
        }

        if (type.IsEnum)
        {
            if (TryParseEnum(type, value, out object? parsed))
            {
                result = (T)parsed!;
                return true;
            }

            return false;
        }

        if (type == typeof(int))
        {
            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(long))
        {
            if (long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out long v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(short))
        {
            if (short.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out short v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(byte))
        {
            if (byte.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out byte v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(sbyte))
        {
            if (sbyte.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out sbyte v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(uint))
        {
            if (uint.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out uint v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(ushort))
        {
            if (ushort.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out ushort v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(ulong))
        {
            if (ulong.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out ulong v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(float))
        {
            if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(double))
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (type == typeof(decimal))
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v)) { result = (T)(object)v; return true; }
            return false;
        }

        if (!CustomTryParseDelegates.TryGetValue(type, out Delegate? parser))
        {
            parser = CreateTryParseDelegate(type);
            CustomTryParseDelegates.TryAdd(type, parser);
        }

        if (parser is null)
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' has no accessible static TryParse(string, out {type.Name}) or " +
                $"TryParse(ReadOnlySpan<char>, out {type.Name}); it cannot be used as a callback pattern capture type.");
        }

        if (parser is TryParseStringDelegate<T> stringParser)
        {
            return stringParser(value, out result);
        }

        return ((TryParseSpanDelegate<T>)parser)(value, out result);
    }

    private static Delegate? CreateTryParseDelegate(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        MethodInfo? method = type.GetMethod("TryParse", flags, binder: null, types: new[] { typeof(string), type.MakeByRefType() }, modifiers: null);
        if (method is not null)
        {
            return Delegate.CreateDelegate(typeof(TryParseStringDelegate<>).MakeGenericType(type), method);
        }

        method = type.GetMethod("TryParse", flags, binder: null, types: new[] { typeof(ReadOnlySpan<char>), type.MakeByRefType() }, modifiers: null);
        return method is null ? null : Delegate.CreateDelegate(typeof(TryParseSpanDelegate<>).MakeGenericType(type), method);
    }
}

/// <summary>
/// Segment view over callback data produced by <see cref="CallbackPatternMatcher.Match(string, char, int, out CallbackSegments)"/>;
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
