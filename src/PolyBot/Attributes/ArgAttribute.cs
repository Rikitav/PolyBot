namespace PolyBot.Attributes;

/// <summary>
/// Binds a handler parameter of a <c>[Command]</c>-restricted handler to one whitespace-separated
/// argument token from the message text, instead of resolving it from the DI container.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ArgAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance without an explicit name; the parameter name is used.
    /// </summary>
    public ArgAttribute()
    {
    }

    /// <param name="name">The argument name, used in parse error messages; <c>null</c> infers the parameter name.</param>
    public ArgAttribute(string? name)
    {
        Name = name;
    }

    /// <summary>
    /// The argument name, used in parse error messages. Inferred from the parameter name when unset.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// When <c>true</c>, a missing token yields the default value of the argument type instead of a parse error. Defaults to <c>false</c>.
    /// </summary>
    public bool IsOptional { get; set; }
}
