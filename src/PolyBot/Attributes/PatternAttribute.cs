using PolyBot.Routing;

namespace PolyBot.Attributes;

/// <summary>
/// Declares a route template over <c>Telegram.Bot.Types.CallbackQuery.Data</c> for a
/// <c>[CallbackQueryHandler]</c> method. Segments are separated by <see cref="Separator"/>
/// (default <c>':'</c>): a literal segment (<c>cart:checkout</c>) must match exactly, a
/// placeholder (<c>{id}</c>, optionally constrained — <c>{id:int}</c>) captures one segment
/// into the <c>[Arg]</c>-annotated parameter of the same name, and a trailing <c>{*name}</c>
/// wildcard captures the rest of the data into a <c>string</c> parameter. Data is at most
/// <see cref="CallbackPatternMatcher.MaxCallbackDataBytes"/> bytes, so templates whose literal
/// skeleton cannot fit are compile-time errors.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PatternAttribute : Attribute
{
    /// <param name="template">The segment template, e.g. <c>"item:{id}:{action}"</c>.</param>
    public PatternAttribute(string template)
    {
        Template = template;
    }

    /// <summary>
    /// The route template.
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// The segment separator. Defaults to <c>':'</c>.
    /// </summary>
    public char Separator { get; set; } = ':';
}
