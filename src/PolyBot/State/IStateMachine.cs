namespace PolyBot.State;

/// <summary>
/// Typed facade over <see cref="IStateStorage"/> scoped to the update currently being
/// routed; keys are resolved from the current update via <see cref="StateKeyResolver"/>.
/// </summary>
public interface IStateMachine
{
    /// <summary>
    /// Gets the current state for the resolved key, or <c>null</c> when there is no
    /// current update, the key is unresolvable for it, or no state is stored.
    /// </summary>
    ValueTask<TEnum?> GetAsync<TEnum>(StateKey keyType = StateKey.UserId, CancellationToken ct = default)
        where TEnum : struct, Enum;

    /// <summary>
    /// Stores <paramref name="state"/> for the resolved key.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No current update or unresolvable key: silent state loss is worse than a visible
    /// failure, so this throws instead of dropping the write.
    /// </exception>
    ValueTask SetAsync<TEnum>(TEnum state, StateKey keyType = StateKey.UserId, CancellationToken ct = default)
        where TEnum : struct, Enum;

    /// <summary>
    /// Removes the stored state for the resolved key, if any.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No current update or unresolvable key: silent state loss is worse than a visible
    /// failure, so this throws instead of dropping the reset.
    /// </exception>
    ValueTask ResetAsync<TEnum>(StateKey keyType = StateKey.UserId, CancellationToken ct = default)
        where TEnum : struct, Enum;
}
