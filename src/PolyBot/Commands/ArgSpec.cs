namespace PolyBot.Commands;

/// <summary>
/// Describes one command argument for <see cref="CommandHelper.ParseArgs"/>: type, name,
/// and whether it may be omitted.
/// </summary>
public sealed class ArgSpec
{
    /// <summary>
    /// Creates the argument specification.
    /// </summary>
    public ArgSpec(Type type, string name, bool isOptional)
    {
        Type = type;
        Name = name;
        IsOptional = isOptional;
    }

    /// <summary>
    /// The type the argument token is parsed into.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The argument name, used in parse error messages.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// When <c>true</c>, a missing token yields the default value of <see cref="Type"/>
    /// instead of a parse error.
    /// </summary>
    public bool IsOptional { get; }
}
