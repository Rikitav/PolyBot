namespace PolyBot.Attributes;

/// <summary>
/// Declares a button of a generated inline keyboard; the source generator emits the
/// implementation. Each attribute list forms one keyboard row.
/// </summary>
/// <remarks>
/// <see cref="CallbackData"/> may contain <c>{parameter}</c> placeholders referencing the declaring
/// method's parameters; partial properties have no parameters, so placeholders only work on
/// methods. Unknown placeholders are reported as <c>CUR017</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
public sealed class CallbackButtonAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute with a button label and callback data.
    /// </summary>
    public CallbackButtonAttribute(string text, string callbackData)
    {
        Text = text;
        CallbackData = callbackData;
    }

    /// <summary>
    /// The button label.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The callback data, optionally with <c>{parameter}</c> placeholders.
    /// </summary>
    public string CallbackData { get; }
}
