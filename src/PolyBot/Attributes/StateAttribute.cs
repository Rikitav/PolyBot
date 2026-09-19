using PolyBot.State;

namespace PolyBot.Attributes;

/// <summary>
/// Restricts a handler to updates whose current <typeparamref name="TEnum"/> state (see
/// <see cref="IStateMachine"/>) equals <see cref="State"/> for <see cref="Key"/>.
/// </summary>
/// <remarks>
/// Attributes sharing the same <c>(TEnum, StateKey)</c> pair are OR-ed (any listed state
/// matches); attributes with different pairs are AND-ed. Handlers with neither
/// <see cref="StateAttribute{TEnum}"/> nor <see cref="NoStateAttribute{TEnum}"/> are fully
/// stateless and always execute when reached.
/// </remarks>
/// <typeparam name="TEnum">The enum type that declares the states.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class StateAttribute<TEnum> : Attribute
    where TEnum : struct, Enum
{
    /// <param name="state">The state the handler matches.</param>
    /// <param name="key">Which identity of the update to key the state by.</param>
    public StateAttribute(TEnum state, StateKey key = StateKey.UserId)
    {
        State = state;
        Key = key;
    }

    /// <summary>
    /// The state the handler matches.
    /// </summary>
    public TEnum State { get; }

    /// <summary>
    /// Which identity of the update the state is keyed by.
    /// </summary>
    public StateKey Key { get; }
}
