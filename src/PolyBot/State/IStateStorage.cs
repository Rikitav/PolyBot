namespace PolyBot.State;

/// <summary>
/// Persistent storage for finite-state-machine states. Callers pass raw identity keys;
/// implementations namespace them internally so states of different enum types never
/// collide for the same raw key.
/// </summary>
public interface IStateStorage
{
    /// <summary>
    /// Gets the state stored for <paramref name="key"/>, or <c>null</c> when none (or an expired one) is stored.
    /// </summary>
    ValueTask<TEnum?> GetStateAsync<TEnum>(string key, CancellationToken ct = default)
        where TEnum : struct, Enum;

    /// <summary>
    /// Stores <paramref name="state"/> for <paramref name="key"/>, replacing any previously stored state.
    /// </summary>
    ValueTask SetStateAsync<TEnum>(string key, TEnum state, CancellationToken ct = default)
        where TEnum : struct, Enum;

    /// <summary>
    /// Removes the state stored for <paramref name="key"/>, if any.
    /// </summary>
    ValueTask ClearStateAsync<TEnum>(string key, CancellationToken ct = default)
        where TEnum : struct, Enum;
}
