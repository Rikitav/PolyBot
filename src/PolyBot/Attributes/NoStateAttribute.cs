using PolyBot.State;

namespace PolyBot.Attributes;

/// <summary>
/// Restricts a handler to updates that have no stored <typeparamref name="TEnum"/> state
/// for <see cref="Key"/>; typically used for "entry" handlers that start a conversation flow.
/// </summary>
/// <remarks>
/// Attributes sharing the same <c>(TEnum, StateKey)</c> pair are OR-ed; attributes with
/// different pairs are AND-ed. An update whose key cannot be resolved does <em>not</em> match:
/// <c>NoState</c> means "the identity was resolved and has no state".
/// </remarks>
/// <typeparam name="TEnum">The enum type that declares the states.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class NoStateAttribute<TEnum> : Attribute
    where TEnum : struct, Enum
{
    /// <param name="key">Which identity of the update to key the state by.</param>
    public NoStateAttribute(StateKey key = StateKey.UserId)
    {
        Key = key;
    }

    /// <summary>
    /// Which identity of the update the state is keyed by.
    /// </summary>
    public StateKey Key { get; }
}
