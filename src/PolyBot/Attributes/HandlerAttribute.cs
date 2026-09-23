namespace PolyBot.Attributes;

/// <summary>
/// Base class for update-type handler attributes (e.g. <c>MessageHandlerAttribute</c>).
/// Marks a method as a handler for a specific kind of update.
/// </summary>
/// <remarks>
/// Concrete, update-specific attributes are generated at compile time by the
/// PolyBot source generator into the <c>PolyBot.Attributes</c> namespace.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class HandlerAttribute : Attribute
{
    /// <summary>
    /// Routing priority; higher values run earlier. Defaults to <c>0</c>.
    /// </summary>
    public int Priority { get; set; } = 0;
}
