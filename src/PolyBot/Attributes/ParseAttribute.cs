using PolyBot.Commands;

namespace PolyBot.Attributes;

/// <summary>
/// Binds a <c>string</c> handler parameter of a <c>[Command]</c>-restricted handler to the
/// match of <see cref="Pattern"/> against the text remaining after the positionally consumed
/// <c>[Arg]</c> tokens and any preceding tail consumers (<c>[Parse]</c>/<c>[Rest]</c> parameters).
/// </summary>
/// <remarks>
/// The pattern is scanned, not anchored: text before the match is skipped and claimed. With no
/// capturing groups the whole match is bound; with one or more, <c>Groups[1].Value</c> is bound.
/// No match throws a <see cref="CommandArgsParseException"/> from <see cref="CommandHelper.ParseArgs"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ParseAttribute : Attribute
{
    /// <param name="pattern">The regular expression scanned against the remaining text.</param>
    public ParseAttribute(string pattern)
    {
        Pattern = pattern;
    }

    /// <summary>
    /// The regular expression scanned (not anchored) against the remaining text.
    /// </summary>
    public string Pattern { get; }
}
